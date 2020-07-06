using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MphRead
{
    public static class Output
    {
        public enum Operation
        {
            Write,
            Read,
            Clear
        }

        private struct QueueItem
        {
            public Operation Operation;
            public string? Message;

            public QueueItem(Operation operation, string? message)
            {
                Operation = operation;
                Message = message;
            }
        }

        private struct QueueInput
        {
            public int Count;
            public string Message;

            public QueueInput(int count, string message)
            {
                Count = count;
                Message = message;
            }
        }

        private static bool _initialized = false;
        private static readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private static readonly SemaphoreSlim _batchLock = new SemaphoreSlim(1, 1);
        private static CancellationTokenSource _cts = null!;
        private static List<QueueItem> _items = null!;
        private static List<QueueInput> _input = null!;

        public static async Task Begin()
        {
            await _lock.WaitAsync();
            if (!_initialized)
            {
                _initialized = true;
                _items = new List<QueueItem>();
                _input = new List<QueueInput>();
                _cts = new CancellationTokenSource();
                _ = Task.Run(async () => await Run(_cts.Token));
            }
            _lock.Release();
        }

        private static Guid? _batchGuid = null;

        public static async Task<Guid> StartBatch()
        {
            await _batchLock.WaitAsync();
            _batchGuid = Guid.NewGuid();
            return _batchGuid.Value;
        }

        public static async Task EndBatch()
        {
            await _lock.WaitAsync();
            _batchGuid = null;
            _batchLock.Release();
            _lock.Release();
        }

        public static async Task Write(Guid? guid = null)
        {
            await Write("", guid);
        }
        
        public static async Task Write(string message, Guid? guid = null)
        {
            await _lock.WaitAsync();
            if (guid != _batchGuid)
            {
                await _batchLock.WaitAsync();
            }
            _items.Add(new QueueItem(Operation.Write, message));
            if (guid != _batchGuid)
            {
                _batchLock.Release();
            }
            _lock.Release();
        }

        public static async Task Clear(Guid? guid = null)
        {
            await _lock.WaitAsync();
            if (guid != _batchGuid)
            {
                await _batchLock.WaitAsync();
            }
            _items.Add(new QueueItem(Operation.Clear, message: null));
            if (guid != _batchGuid)
            {
                _batchLock.Release();
            }
            _lock.Release();
        }

        public static async Task<string> Read(string? message = null, Guid? guid = null)
        {
            await _lock.WaitAsync();
            if (guid != _batchGuid)
            {
                await _batchLock.WaitAsync();
            }
            _items.Add(new QueueItem(Operation.Read, message));
            int count = _input.Count;
            if (guid != _batchGuid)
            {
                _batchLock.Release();
            }
            _lock.Release();
            return await DoRead(count);
        }

        private static async Task<string> DoRead(int count)
        {
            while (true)
            {
                await _lock.WaitAsync();
                if (_input.Count > 0 && _input[0].Count == count)
                {
                    string input = _input[0].Message;
                    _input.RemoveAt(0);
                    _lock.Release();
                    return input;
                }
                _lock.Release();
                await Task.Delay(100);
            }
        }

        private static async Task Run(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await _lock.WaitAsync();
                while (_items.Count > 0)
                {
                    QueueItem item = _items[0];
                    if (item.Operation == Operation.Write)
                    {
                        Console.WriteLine(item.Message);
                    }
                    else if (item.Operation == Operation.Clear)
                    {
                        Console.Clear();
                    }
                    else if (item.Operation == Operation.Read)
                    {
                        if (item.Message != null)
                        {
                            Console.Write(item.Message);
                            _input.Add(new QueueInput(_input.Count, Console.ReadLine()));
                        }
                    }
                    _items.RemoveAt(0);
                }
                _lock.Release();
                await Task.Delay(100);
            }
        }

        public static async Task End()
        {
            await _lock.WaitAsync();
            if (_initialized)
            {
                _cts.Cancel();
            }
            _lock.Release();
        }
    }
}
