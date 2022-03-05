using System.Collections.Generic;
using MphRead.Entities;

namespace MphRead
{
    public readonly struct MessageInfo
    {
        public readonly Message Message;
        public readonly EntityBase Sender;
        public readonly EntityBase? Target;
        public readonly object Param1;
        public readonly object Param2;
        public readonly ulong ExecuteFrame;
        public readonly ulong QueuedFrame;

        public MessageInfo(Message message, EntityBase sender, EntityBase? target, object param1, object param2,
            ulong executeFrame, ulong queuedFrame)
        {

            Message = message;
            Sender = sender;
            Target = target;
            Param1 = param1;
            Param2 = param2;
            ExecuteFrame = executeFrame;
            QueuedFrame = queuedFrame;
        }
    }

    public partial class Scene
    {
        private const int _queueSize = 40;
        private readonly List<MessageInfo> _queue = new List<MessageInfo>(_queueSize);
        public IReadOnlyList<MessageInfo> MessageQueue => _queue;

        public void SendMessage(Message message, EntityBase sender, EntityBase? target, object param1, object param2)
        {
            ulong frame = _frameCount;
            if (target == null)
            {
                frame++;
            }
            DispatchOrQueueMessage(message, sender, target, param1, param2, frame);
        }

        public void SendMessage(Message message, EntityBase sender, EntityBase? target, object param1, object param2, int delay)
        {
            if (delay < 0)
            {
                delay = 0;
            }
            DispatchOrQueueMessage(message, sender, target, param1, param2, _frameCount + (ulong)delay);
        }

        private void DispatchOrQueueMessage(Message message, EntityBase sender, EntityBase? target, object param1, object param2, ulong frame)
        {
            var info = new MessageInfo(message, sender, target, param1, param2, frame, _frameCount);
            if (frame <= _frameCount)
            {
                DispatchMessage(info);
            }
            else
            {
                QueueMessage(info);
            }
        }

        private void DispatchMessage(MessageInfo info)
        {
            if (info.Message == Message.SetTriggerState || info.Message == Message.ClearTriggerState)
            {
                // todo: this
            }
            else if (info.Target != null)
            {
                info.Target.HandleMessage(info);
            }
        }

        private void QueueMessage(MessageInfo info)
        {
            if (_queue.Count < _queueSize)
            {
                _queue.Add(info);
            }
        }

        private void ProcessMessageQueue()
        {
            for (int i = 0; i < _queue.Count; i++)
            {
                MessageInfo info = _queue[i];
                if (info.ExecuteFrame <= _frameCount)
                {
                    DispatchMessage(info);
                    _queue.RemoveAt(i);
                    i--;
                }
            }
        }

        public void ClearMessageQueue()
        {
            _queue.Clear();
        }
    }
}
