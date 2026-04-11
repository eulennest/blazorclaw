using BlazorClaw.Core.Sessions;
using System.Collections;

namespace BlazorClaw.Core.Services
{
    public class ChannelRegistry(IMessageDispatcher messageDispatcher) : ICollection<IChannelBot>
    {
        private readonly List<IChannelBot> bots = [];

        public int Count => ((ICollection<IChannelBot>)bots).Count;

        public bool IsReadOnly => ((ICollection<IChannelBot>)bots).IsReadOnly;

        public void Add(IChannelBot item)
        {
            messageDispatcher.Register(item);
            bots.Add(item);
        }

        public void Clear()
        {
            foreach (var item in bots)
            {
                messageDispatcher.Unregister(item);
            }
            bots.Clear();
        }

        public bool Contains(IChannelBot item)
        {
            return bots.Contains(item);
        }

        public void CopyTo(IChannelBot[] array, int arrayIndex)
        {
            bots.CopyTo(array, arrayIndex);
        }

        public IEnumerator<IChannelBot> GetEnumerator()
        {
            return bots.ToList().GetEnumerator();
        }

        public bool Remove(IChannelBot item)
        {
            messageDispatcher.Unregister(item);
            return bots.Remove(item);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return bots.ToArray().GetEnumerator();
        }
    }

}
