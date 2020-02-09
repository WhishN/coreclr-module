using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace AltV.Net.EntitySync
{
    public class Entity : IEntity
    {
        public ulong Id { get; }
        public ulong Type { get; }

        private Vector3 position;

        public Vector3 Position
        {
            get => position;
            set => SetPositionInternal(value);
        }

        private int positionState = (int) EntityPropertyState.PropertyNotChanged;

        private Vector3 newPosition;

        private int dimension;

        public int Dimension
        {
            get => dimension;
            set => SetDimensionInternal(value);
        }

        private int dimensionState = (int) EntityPropertyState.PropertyNotChanged;

        private int newDimension;

        public uint Range { get; }
        public object FlagsMutex { get; } = new object();
        public int Flags { get; set; }

        private readonly IDictionary<string, object> data;

        private readonly EntityDataSnapshot dataSnapshot;

        /// <summary>
        /// List of clients that have the entity created.
        /// </summary>
        private readonly HashSet<IClient> clients = new HashSet<IClient>();

        /// <summary>
        /// List of clients that had the entity created last time, so we can calculate when a client is not in range anymore.
        /// </summary>
        private readonly IDictionary<IClient, bool> lastCheckedClients = new Dictionary<IClient, bool>();

        public Entity(ulong type, Vector3 position, uint range) : this(
            AltEntitySync.IdProvider.GetNext(), type,
            position, range, new Dictionary<string, object>())
        {
        }

        public Entity(ulong type, Vector3 position, uint range, IDictionary<string, object> data) : this(
            AltEntitySync.IdProvider.GetNext(), type,
            position, range, data)
        {
        }

        internal Entity(ulong id, ulong type, Vector3 position, uint range, IDictionary<string, object> data)
        {
            Id = id;
            Type = type;
            this.position = position;
            Range = range;
            dataSnapshot = new EntityDataSnapshot(Id);
            this.data = data;
        }

        public void SetData(string key, object value)
        {
            lock (data)
            {
                dataSnapshot.Update(key);
                data[key] = value;
            }
        }

        public void ResetData(string key)
        {
            lock (data)
            {
                dataSnapshot.Update(key);
                data.Remove(key);
            }
        }

        public bool TryGetData(string key, out object value)
        {
            lock (data)
            {
                return data.TryGetValue(key, out value);
            }
        }

        public bool TryGetData<T>(string key, out T value)
        {
            lock (data)
            {
                if (!data.TryGetValue(key, out var currValue))
                {
                    value = default;
                    return false;
                }

                if (!(currValue is T correctValue))
                {
                    value = default;
                    return false;
                }

                value = correctValue;
                return true;
            }
        }

        /// <summary>
        /// Tries to add a client to the list of clients that created this entity.
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        public bool TryAddClient(IClient client)
        {
            return clients.Add(client);
        }

        public bool RemoveClient(IClient client)
        {
            lastCheckedClients.Remove(client);
            return clients.Remove(client);
        }

        public void AddCheck(IClient client)
        {
            lastCheckedClients[client] = true;
        }

        public void RemoveCheck(IClient client)
        {
            lastCheckedClients[client] = false;
        }

        public IDictionary<IClient, bool> GetLastCheckedClients()
        {
            return lastCheckedClients;
        }

        public HashSet<IClient> GetClients()
        {
            return clients;
        }

        public void SetPositionInternal(Vector3 currNewPosition)
        {
            lock (FlagsMutex)
            {
                positionState = (int) EntityPropertyState.PropertyChanged;
                newPosition = currNewPosition;
            }
        }

        public bool TrySetPositionComputing(out Vector3 currNewPosition)
        {
            lock (FlagsMutex)
            {
                if (positionState != (int) EntityPropertyState.PropertyChanged)
                {
                    currNewPosition = default;
                    return false;
                }

                currNewPosition = newPosition;
                positionState = (int) EntityPropertyState.PropertyChangeComputing;
            }

            return true;
        }

        public void SetPositionComputed()
        {
            lock (FlagsMutex)
            {
                if (positionState != (int) EntityPropertyState.PropertyChangeComputing) return;
                positionState = (int) EntityPropertyState.PropertyChangeComputed;
                position = newPosition;
            }
        }

        public void SetDimensionInternal(int currNewDimension)
        {
            lock (FlagsMutex)
            {
                dimensionState = (int) EntityPropertyState.PropertyChanged;
                newDimension = currNewDimension;
            }
        }

        public bool TrySetDimensionComputing(out int currNewDimension)
        {
            lock (FlagsMutex)
            {
                if (dimensionState != (int) EntityPropertyState.PropertyChanged)
                {
                    currNewDimension = default;
                    return false;
                }

                currNewDimension = newDimension;
                dimensionState = (int) EntityPropertyState.PropertyChangeComputing;
            }

            return true;
        }

        public void SetDimensionComputed()
        {
            lock (FlagsMutex)
            {
                if (dimensionState != (int) EntityPropertyState.PropertyChangeComputing) return;
                dimensionState = (int) EntityPropertyState.PropertyChangeComputed;
                dimension = newDimension;
            }
        }

        public virtual byte[] Serialize(IEnumerable<string> changedKeys)
        {
            using var m = new MemoryStream();
            using (var writer = new BinaryWriter(m))
            {
                writer.Write(Id);
                writer.Write(Type);
                writer.Write(position.X);
                writer.Write(position.Y);
                writer.Write(position.Z);
                writer.Write(Range);
                //TODO: serialize data depending on changedKeys
            }

            return m.ToArray();
        }

        public IEnumerable<string> CompareSnapshotWithClient(IClient client)
        {
            return dataSnapshot.CompareWithClient(client);
        }
    }
}