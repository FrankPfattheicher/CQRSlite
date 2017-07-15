﻿using CQRSlite.Domain.Exception;
using CQRSlite.Events;
using CQRSlite.Infrastructure;
using System;
using System.Collections.Generic;

namespace CQRSlite.Domain
{
    public abstract class AggregateRoot
    {
        private readonly List<IEvent> _changes = new List<IEvent>();

        public Guid Id { get; protected set; }
        public int Version { get; protected set; }

        public IEvent[] GetUncommittedChanges()
        {
            lock (_changes)
            {
                return _changes.ToArray();
            }
        }

        public IEvent[] FlushUncommitedChanges()
        {
            lock (_changes)
            {
                var changes = _changes.ToArray();
                var i = 0;
                foreach (var @event in changes)
                {
                    if (@event.Id == Guid.Empty && Id == Guid.Empty)
                    {
                        throw new AggregateOrEventMissingIdException(GetType(), @event.GetType());
                    }
                    if (@event.Id == Guid.Empty)
                    {
                        @event.Id = Id;
                    }
                    i++;
                    @event.Version = Version + i;
                    @event.TimeStamp = DateTimeOffset.UtcNow;
                }
                Version = Version + _changes.Count;
                _changes.Clear();
                return changes;
            }
        }

        public void LoadFromHistory(IEnumerable<IEvent> history)
        {
            foreach (var e in history)
            {
                if (e.Version != Version + 1)
                {
                    throw new EventsOutOfOrderException(e.Id);
                }
                ApplyChange(e, false);
            }
        }

        protected void ApplyChange(IEvent @event)
        {
            ApplyChange(@event, true);
        }

        /// <summary>
        /// Allow a derived class to provide a separate implementation of calling domain class event handlers
        /// </summary>
        /// <param name="event">the event</param>
        protected virtual void OnApply(IEvent @event)
        {
            this.AsDynamic().Apply(@event);
        }

        private void ApplyChange(IEvent @event, bool isNew)
        {
            lock (_changes)
            {
                OnApply(@event);
                if (isNew)
                {
                    _changes.Add(@event);
                }
                else
                {
                    Id = @event.Id;
                    Version++;
                }
            }
        }
    }
}
