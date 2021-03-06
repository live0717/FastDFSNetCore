using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace FastDFS.Client
{
    internal class Pool
    {
        // Fields
        private readonly SemaphoreSlim _autoEvent;
        private readonly IPEndPoint _endPoint;
        private readonly Stack<Connection> _idle;
        private readonly List<Connection> _inUse;
        private readonly int _maxConnection;
        private readonly object _locker = new object();

        // Methods
        public Pool(IPEndPoint endPoint, int maxConnection)
        {
            this._autoEvent = new SemaphoreSlim(0);
            this._inUse = new List<Connection>(maxConnection);
            this._idle = new Stack<Connection>(maxConnection);
            this._maxConnection = maxConnection;
            this._endPoint = endPoint;

        }

        public void CloseConnection(Connection conn)
        {
            conn.InUse = false;
            lock (_locker)
            {
                this._inUse.Remove(conn);
            }
            lock (_locker)
            {
                this._idle.Push(conn);
            }
            this._autoEvent.Release();
        }

        public Connection GetConnection()
        {
            int millisecondsTimeout = FDFSConfig.GetConnectionTimeout * 1000;
            while (true)
            {
                var pooldConncetion = this.GetPooldConncetion();
                if (pooldConncetion != null)
                {
                    return pooldConncetion;
                }
                if (!this._autoEvent.Wait(millisecondsTimeout))
                {
                    break;
                }
            }
            throw new FDFSException("Get CanUse Connection Time Out");
        }


        public async Task<Connection> GetConnectionAsync()
        {
            int millisecondsTimeout = FDFSConfig.GetConnectionTimeout * 1000;
            while (true)
            {
                var pooldConncetion = this.GetPooldConncetion();
                if (pooldConncetion != null)
                {
                    return pooldConncetion;
                }
                if (!await this._autoEvent.WaitAsync(millisecondsTimeout))
                {
                    break;
                }
            }
            throw new FDFSException("Get CanUse Connection Time Out");
        }

        private Connection GetPooldConncetion()
        {
            Connection item = null;
            lock (_locker)
            {
                if (this._idle.Count > 0)
                {
                    item = this._idle.Pop();
                }
            }
            lock (_locker)
            {
                if (this._inUse.Count == this._maxConnection)
                {
                    return null;
                }
                if (item == null)
                {
                    item = new Connection(_endPoint)
                    {
                        Pool = this
                    };
                }
                this._inUse.Add(item);
            }
            return item;
        }
    }
}