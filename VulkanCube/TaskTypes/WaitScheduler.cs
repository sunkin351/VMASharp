using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.KHR;

namespace VulkanCube.TaskTypes
{
    public class WaitScheduler : IDisposable
    {
        private static Vk VkApi => InstanceCreationExample.VkApi;

        private readonly ConcurrentQueue<(Fence, TaskCompletionSource)> _fenceQueue = new();

        private readonly Thread _schedulerThread;

        private readonly Device _device;

        private volatile int _state = 0;

        public WaitScheduler(Device device)
        {
            _device = device;

            _schedulerThread = new Thread(ScheduleThreadMethod);
            _schedulerThread.Start();
        }

        public Task WaitForFenceAsync(Fence fence)
        {
            var res = VkApi.GetFenceStatus(_device, fence);

            switch (res)
            {
                case Result.Success:
                    return Task.CompletedTask;
                case Result.NotReady:
                    CheckState();

                    var tcs = new TaskCompletionSource();

                    _fenceQueue.Enqueue((fence, tcs));

                    return tcs.Task;
                default:
                    throw new Exception(res.ToString());
            }
        }

        private void CheckState()
        {
            int tmp = _state;

            if (tmp != 0)
            {
                throw new InvalidOperationException($"{(Result)tmp}");
            }
        }

        private unsafe void ScheduleThreadMethod()
        {
            var list = new UnmanagedList<Fence>(64);
            var taskList = new List<TaskCompletionSource>(64);
            Result res;

            while (_state == 0)
            {
                while (_fenceQueue.TryDequeue(out var tuple))
                {
                    var (fence, tcs) = tuple;

                    res = VkApi.GetFenceStatus(_device, fence);

                    switch (res)
                    {
                        case Result.Success:
                            tcs.SetResult();
                            break;
                        case Result.NotReady:
                            list.Add(fence);
                            taskList.Add(tcs);
                            break;
                        default:
                            tcs.SetException(new Exception("VkGetFenceStatus() returned " + res));
                            break;
                    }

                }

                if (list.Count == 0)
                {
                    Thread.Sleep(1);
                    continue;
                }

                res = VkApi.WaitForFences(_device, (uint)list.Count, list.BasePointer, false, 5 * 1_000_000);

                switch (res)
                {
                    case Result.Timeout:
                        break;
                    case Result.Success:

                        for (int i = 0; i < list.Count;)
                        {
                            var fence = list[i];
                            var source = taskList[i];

                            res = VkApi.GetFenceStatus(_device, fence);

                            if (res != Result.NotReady)
                            {
                                list.RemoveAt(i);
                                taskList.RemoveAt(i);

                                if (res == Result.Success)
                                {
                                    source.SetResult();
                                }
                                else
                                {
                                    source.SetException(new Exception("VkGetFenceStatus() returned " + res));
                                }

                                continue; //Skip index increment
                            }
                            
                            i += 1;
                        }

                        break;
                    default:
                        _state = (int)res;

                        string resMessage = "Unhandled Error: " + res;

                        foreach (var source in taskList)
                        {
                            source.SetException(new Exception(resMessage));
                        }
                        break;
                }
            }
        }

        public void Dispose()
        {
            _state = 1;

            this._schedulerThread.Join();
        }

        private struct UnmanagedList<T> where T: unmanaged
        {
            private T[] _array;
            private int _count;

            public UnmanagedList(int capacity)
            {
                _array = GC.AllocateArray<T>(capacity, true);
                _count = 0;
            }

            public int Count => _count;

            public unsafe T* BasePointer => (T*)Unsafe.AsPointer(ref MemoryMarshal.GetArrayDataReference(_array));

            public T this[int idx]
            {
                get => _array[idx];
            }

            public void Add(T item)
            {
                if (_count >= _array.Length)
                {
                    var newlen = _array.Length * 2;

                    var newArr = GC.AllocateArray<T>(newlen, true);

                    Array.Copy(_array, newArr, _array.Length);

                    _array = newArr;
                }

                _array[_count++] = item;
            }

            public void RemoveAt(int idx)
            {
                if ((uint)idx >= (uint)_count)
                {
                    throw new IndexOutOfRangeException();
                }

                if (idx != _count - 1)
                {
                    Array.Copy(_array, idx + 1, _array, idx, _count - idx - 1);
                }

                _count -= 1;
            }
        }
    }
}
