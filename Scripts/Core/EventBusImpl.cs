using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEventBus.Utils;


namespace UnityEventBus
{
    /// <summary>
    /// EventBus functionality
    /// </summary>
    public class EventBusImpl : IEventBusImpl
    {
        private const int k_DefaultSetSize = 4;

        private Dictionary<Type, SortedCollection<ListenerWrapper>> m_Listeners = new Dictionary<Type, SortedCollection<ListenerWrapper>>();
        private SortedCollection<BusWrapper>                        m_Buses     = new SortedCollection<BusWrapper>(BusWrapper.k_OrderComparer);

        // =======================================================================
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Send<TEvent, TInvoker>(in TEvent e, in TInvoker invoker)
            where TInvoker : IEventInvoker
        {
            // propagate, invoke listeners
            if (m_Listeners.TryGetValue(typeof(IListener<TEvent>), out var set))
            {
                // set can be modified through execution
                foreach (var listenerWrapper in set.ToArray())
                {
#if  DEBUG
                    if (listenerWrapper.IsActive)
                        invoker.Invoke(in e, in listenerWrapper.Listener);
#else
                    try
                    {
                        if (listenerWrapper.IsActive)
                            invoker.Invoke(in e, in listenerWrapper.Listener);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError($"Listener: id: {listenerWrapper.Name}, key: {listenerWrapper.KeyType}; Exception: {exception}");
                    }
#endif
                }
            }

            // to buses
            if (m_Buses.Count != 0)
            {
                foreach (var bus in m_Buses.ToArray())
                {
                    if (bus.IsActive)
                        bus.Bus.Send(in e, in invoker);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Subscribe(ListenerWrapper listener)
        {
            if (listener == null)
                throw new ArgumentNullException(nameof(listener));

            // get or create group
            if (m_Listeners.TryGetValue(listener.KeyType, out var set) == false)
            {
                set = new SortedCollection<ListenerWrapper>(ListenerWrapper.k_OrderComparer, k_DefaultSetSize);
                m_Listeners.Add(listener.KeyType, set);
            }

            if (set.Contains(listener) == false)
                set.Add(listener);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnSubscribe(ListenerWrapper listener)
        {
            if (listener == null)
                throw new ArgumentNullException(nameof(listener));

            // remove first match
            if (m_Listeners.TryGetValue(listener.KeyType, out var set))
            {
                // remove & dispose
                if (set.Extract(in listener, out var extracted))
                    extracted.Dispose();

                if (set.Count == 0)
                    m_Listeners.Remove(listener.KeyType);
            }

            listener.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Subscribe(IEventBus bus)
        {
            if (bus == null)
                throw new ArgumentNullException(nameof(bus));

            m_Buses.Add(BusWrapper.Create(in bus));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UnSubscribe(IEventBus bus)
        {
            if (bus == null)
                throw new ArgumentNullException(nameof(bus));

            var busWrapper = BusWrapper.Create(in bus);
            if (m_Buses.Extract(busWrapper, out var extracted))
                extracted.Dispose();

            busWrapper.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IEnumerable<ListenerWrapper> GetListeners()
        {
            return m_Listeners.SelectMany(group => group.Value);
        }

        public void Dispose()
        {
            foreach (var wrappers in m_Listeners.Values)
            foreach (var wrapper in wrappers)
                wrapper.Dispose();
        }
    }
}