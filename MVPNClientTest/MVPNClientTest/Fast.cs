using System;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

#pragma warning disable CS0162

namespace SoftEther.WebSocket.Helper
{
    struct FastReadList<T>
    {
        static object GlobalWriteLock = new object();
        static volatile int IdSeed = 0;

        SortedDictionary<int, T> Hash;

        volatile T[] InternalFastList;

        public T[] GetListFast() => InternalFastList;

        public int Add(T value)
        {
            lock (GlobalWriteLock)
            {
                if (Hash == null)
                    Hash = new SortedDictionary<int, T>();

                int id = ++IdSeed;
                Hash.Add(id, value);
                Update();
                return id;
            }
        }

        public bool Delete(int id)
        {
            lock (GlobalWriteLock)
            {
                if (Hash == null)
                    return false;

                bool ret = Hash.Remove(id);
                if (ret)
                {
                    Update();
                }
                return ret;
            }
        }

        void Update()
        {
            if (Hash.Count == 0)
                InternalFastList = null;
            else
                InternalFastList = Hash.Values.ToArray();
        }
    }

    static class AsyncCleanuperLadyHelper
    {
        public static T AddToLady<T>(this T obj, AsyncCleanuperLady lady) where T : IDisposable
        {
            if (obj == null || lady == null) return obj;
            lady.Add(obj);
            return obj;
        }

        public static T AddToLady<T>(this T obj, AsyncCleanupable cleanupable) where T : IDisposable
        {
            if (obj == null || cleanupable == null) return obj;
            cleanupable.Lady.Add(obj);
            return obj;
        }
    }

    class AsyncCleanuperLady
    {
        static volatile int IdSeed;

        public int Id { get; }

        public AsyncCleanuperLady()
        {
            Id = Interlocked.Increment(ref IdSeed);
        }

        Queue<AsyncCleanuper> CleanuperQueue = new Queue<AsyncCleanuper>();
        Queue<Task> TaskQueue = new Queue<Task>();
        Queue<IDisposable> DisposableQueue = new Queue<IDisposable>();

        void InternalCollectMain(object obj)
        {
            IAsyncCleanupable cleanupable = obj as IAsyncCleanupable;
            AsyncCleanuper cleanuper = obj as AsyncCleanuper;
            Task task = obj as Task;
            IDisposable disposable = obj as IDisposable;

            lock (LockObj)
            {
                if (cleanupable != null) CleanuperQueue.Enqueue(cleanupable.AsyncCleanuper);
                if (cleanuper != null) CleanuperQueue.Enqueue(cleanuper);
                if (task != null) TaskQueue.Enqueue(task);
                if (disposable != null) DisposableQueue.Enqueue(disposable);
            }

            if (IsDisposed)
                DisposeAllSafe();
        }

        public void Add(IAsyncCleanupable cleanupable) => InternalCollectMain(cleanupable);
        public void Add(AsyncCleanuper cleanuper) => InternalCollectMain(cleanuper);
        public void Add(Task task) => InternalCollectMain(task);
        public void Add(IDisposable disposable) => InternalCollectMain(disposable);

        public void MergeFrom(AsyncCleanuperLady fromLady)
        {
            lock (LockObj)
            {
                lock (fromLady.LockObj)
                {
                    fromLady.CleanuperQueue.ToList().ForEach(x => this.CleanuperQueue.Enqueue(x));
                    fromLady.TaskQueue.ToList().ForEach(x => this.TaskQueue.Enqueue(x));
                    fromLady.DisposableQueue.ToList().ForEach(x => this.DisposableQueue.Enqueue(x));

                    fromLady.CleanuperQueue.Clear();
                    fromLady.TaskQueue.Clear();
                    fromLady.DisposableQueue.Clear();
                }
            }
        }

        public void MergeTo(AsyncCleanuperLady toLady)
            => toLady.MergeFrom(this);

        CriticalSection LockObj = new CriticalSection();
        volatile bool _disposed = false;
        public bool IsDisposed { get => _disposed; }

        public void DisposeAllSafe()
        {
            _disposed = true;

            IDisposable[] disposableList;
            lock (LockObj)
            {
                disposableList = DisposableQueue.Reverse().ToArray();
            }

            foreach (var disposable in disposableList)
                disposable.DisposeSafe();
        }

        volatile bool _cleanuped = false;
        public bool IsCleanuped { get => _cleanuped; }

        public async Task CleanupAsync()
        {
            _disposed = true;
            _cleanuped = true;

            AsyncCleanuper[] cleanuperList;
            Task[] taskList;
            IDisposable[] disposableList;

            lock (LockObj)
            {
                cleanuperList = CleanuperQueue.Reverse().ToArray();
                taskList = TaskQueue.Reverse().ToArray();
                disposableList = DisposableQueue.Reverse().ToArray();

                CleanuperQueue.Clear();
                TaskQueue.Clear();
                DisposableQueue.Clear();
            }

            foreach (var disposable in disposableList)
                disposable.DisposeSafe();

            foreach (var cleanuper in cleanuperList)
                await cleanuper;

            foreach (var task in taskList)
            {
                try
                {
                    await task;
                }
                catch { }
            }
        }

        public TaskAwaiter GetAwaiter()
            => CleanupAsync().GetAwaiter();
    }

    interface IAsyncCleanupable : IDisposable
    {
        AsyncCleanuper AsyncCleanuper { get; }
        Task _CleanupAsyncInternal();
    }

    abstract class AsyncCleanupable : IAsyncCleanupable
    {
        public AsyncCleanuper AsyncCleanuper { get; }
        protected internal AsyncCleanuperLady Lady;

        CriticalSection LockObj = new CriticalSection();

        Stack<Action> OnDisposeStack = new Stack<Action>();

        public AsyncCleanupable(AsyncCleanuperLady lady)
        {
            if (lady == null)
                throw new ArgumentNullException("lady == null");

            Lady = lady;
            this.AsyncCleanuper = new AsyncCleanuper(this);
            Lady.Add(this);
        }

        Once DisposeFlag;
        public void Dispose() => Dispose(true);
        protected virtual void Dispose(bool disposing)
        {
            if (disposing && DisposeFlag.IsFirstCall())
            {
                Action[] actions;

                lock (LockObj)
                {
                    actions = OnDisposeStack.ToArray();
                }

                foreach (var action in actions)
                {
                    try
                    {
                        action();
                    }
                    catch { }
                }

                Lady.DisposeAllSafe();
            }
        }

        public virtual async Task _CleanupAsyncInternal()
        {
            await Lady;
        }

        public void AddOnDispose(Action action)
        {
            if (action != null)
                lock (LockObj)
                    OnDisposeStack.Push(action);
        }
    }

    class AsyncCleanuper : IDisposable
    {
        IAsyncCleanupable Target { get; }

        public AsyncCleanuper(IAsyncCleanupable targetObject)
        {
            Target = targetObject;
        }

        Task internalCleanupTask = null;
        CriticalSection LockObj = new CriticalSection();

        public Task CleanupAsync()
        {
            Target.DisposeSafe();

            lock (LockObj)
            {
                if (internalCleanupTask == null)
                    internalCleanupTask = Target._CleanupAsyncInternal().TryWaitAsync(true);
            }

            return internalCleanupTask;
        }

        public TaskAwaiter GetAwaiter()
            => CleanupAsync().GetAwaiter();

        public void Dispose() { }
    }

    delegate bool TimeoutDetectorCallback(TimeoutDetector detector);

    class TimeoutDetector : AsyncCleanupable
    {
        Task mainLoop;

        CriticalSection LockObj = new CriticalSection();

        public long Timeout { get; }

        long NextTimeout;

        AsyncAutoResetEvent ev = new AsyncAutoResetEvent();

        AsyncAutoResetEvent eventAuto;
        AsyncManualResetEvent eventManual;

        CancellationTokenSource halt = new CancellationTokenSource();

        CancelWatcher cancelWatcher;

        CancellationTokenSource cts = new CancellationTokenSource();
        public CancellationToken Cancel { get => cts.Token; }
        public Task TaskWaitMe { get => this.mainLoop; }

        public object UserState { get; }

        public bool TimedOut { get; private set; } = false;

        TimeoutDetectorCallback Callback;

        public TimeoutDetector(AsyncCleanuperLady lady, int timeout, CancelWatcher watcher = null, AsyncAutoResetEvent eventAuto = null, AsyncManualResetEvent eventManual = null,
            TimeoutDetectorCallback callback = null, object userState = null)
            : base(lady)
        {
            if (timeout == System.Threading.Timeout.Infinite || timeout == int.MaxValue)
            {
                return;
            }

            this.Timeout = timeout;
            this.cancelWatcher = watcher.AddToLady(this);
            this.eventAuto = eventAuto;
            this.eventManual = eventManual;
            this.Callback = callback;
            this.UserState = userState;

            NextTimeout = FastTick64.Now + this.Timeout;
            mainLoop = TimeoutDetectorMainLoop().AddToLady(this);
        }

        public void Keep()
        {
            Interlocked.Exchange(ref this.NextTimeout, FastTick64.Now + this.Timeout);
        }

        async Task TimeoutDetectorMainLoop()
        {
            using (LeakChecker.Enter())
            {
                while (true)
                {
                    long nextTimeout = Interlocked.Read(ref this.NextTimeout);

                    long now = FastTick64.Now;

                    long remainTime = nextTimeout - now;

                    if (remainTime <= 0)
                    {
                        if (Callback != null && Callback(this))
                        {
                            Keep();
                        }
                        else
                        {
                            this.TimedOut = true;
                        }
                    }

                    if (this.TimedOut || halt.IsCancellationRequested)
                    {
                        cts.TryCancelAsync().LaissezFaire();

                        if (this.cancelWatcher != null) this.cancelWatcher.Cancel();
                        if (this.eventAuto != null) this.eventAuto.Set();
                        if (this.eventManual != null) this.eventManual.Set();

                        return;
                    }
                    else
                    {
                        await WebSocketHelper.WaitObjectsAsync(
                            events: new AsyncAutoResetEvent[] { ev },
                            cancels: new CancellationToken[] { halt.Token },
                            timeout: (int)remainTime);
                    }
                }
            }
        }

        Once DisposeFlag;
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!disposing || DisposeFlag.IsFirstCall() == false) return;
                cancelWatcher.DisposeSafe();
                halt.TryCancelAsync().LaissezFaire();
            }
            finally { base.Dispose(disposing); }
        }
    }

    class Holder<T> : IDisposable
    {
        public T Value { get; }
        Action<T> DisposeProc;
        LeakCheckerHolder Leak;

        public Holder(Action<T> disposeProc, T value = default(T))
        {
            this.Value = value;
            this.DisposeProc = disposeProc;

            Leak = LeakChecker.Enter();
        }

        Once DisposeFlag;
        public void Dispose() => Dispose(true);
        protected virtual void Dispose(bool disposing)
        {
            if (disposing && DisposeFlag.IsFirstCall())
            {
                try
                {
                    DisposeProc(Value);
                }
                finally
                {
                    Leak.DisposeSafe();
                }
            }
        }
    }

    class Holder : IDisposable
    {
        Action DisposeProc;
        LeakCheckerHolder Leak;

        public Holder(Action disposeProc)
        {
            this.DisposeProc = disposeProc;

            Leak = LeakChecker.Enter();
        }

        Once DisposeFlag;
        public void Dispose() => Dispose(true);
        protected virtual void Dispose(bool disposing)
        {
            if (disposing && DisposeFlag.IsFirstCall())
            {
                try
                {
                    DisposeProc();
                }
                finally
                {
                    Leak.DisposeSafe();
                }
            }
        }
    }

    class AsyncHolder<T> : IAsyncCleanupable
    {
        public AsyncCleanuper AsyncCleanuper { get; }

        public T UserData { get; }
        Action<T> DisposeProc;
        Func<T, Task> AsyncCleanupProc;
        LeakCheckerHolder Leak;
        LeakCheckerHolder Leak2;

        public AsyncHolder(Func<T, Task> asyncCleanupProc, Action<T> disposeProc = null, T userData = default(T))
        {
            this.UserData = userData;
            this.DisposeProc = disposeProc;
            this.AsyncCleanupProc = asyncCleanupProc;

            Leak = LeakChecker.Enter();
            Leak2 = LeakChecker.Enter();
            AsyncCleanuper = new AsyncCleanuper(this);
        }

        Once DisposeFlag;

        public void Dispose() => Dispose(true);
        protected virtual void Dispose(bool disposing)
        {
            if (disposing && DisposeFlag.IsFirstCall())
            {
                try
                {
                    if (DisposeProc != null)
                        DisposeProc(UserData);
                }
                finally
                {
                    Leak.DisposeSafe();
                }
            }
        }

        public async Task _CleanupAsyncInternal()
        {
            try
            {
                await AsyncCleanupProc(UserData);
            }
            finally
            {
                Leak2.DisposeSafe();
            }
        }

        public TaskAwaiter GetAwaiter()
            => AsyncCleanuper.GetAwaiter();
    }

    class AsyncHolder : IAsyncCleanupable
    {
        public AsyncCleanuper AsyncCleanuper { get; }

        Action DisposeProc;
        Func<Task> AsyncCleanupProc;
        LeakCheckerHolder Leak;
        LeakCheckerHolder Leak2;

        public AsyncHolder(Func<Task> asyncCleanupProc, Action disposeProc = null)
        {
            this.DisposeProc = disposeProc;
            this.AsyncCleanupProc = asyncCleanupProc;

            Leak = LeakChecker.Enter();
            Leak2 = LeakChecker.Enter();
            AsyncCleanuper = new AsyncCleanuper(this);
        }

        Once DisposeFlag;

        public void Dispose() => Dispose(true);
        protected virtual void Dispose(bool disposing)
        {
            if (disposing && DisposeFlag.IsFirstCall())
            {
                try
                {
                    if (DisposeProc != null)
                        DisposeProc();
                }
                finally
                {
                    Leak.DisposeSafe();
                }
            }
        }

        public async Task _CleanupAsyncInternal()
        {
            try
            {
                await AsyncCleanupProc();
            }
            finally
            {
                Leak2.DisposeSafe();
            }
        }

        public TaskAwaiter GetAwaiter()
            => AsyncCleanuper.GetAwaiter();
    }

    class GroupManager<TKey, TGroupContext> : IDisposable
    {
        public class GroupHandle : Holder<GroupInstance>
        {
            public TKey Key { get; }
            public TGroupContext Context { get; }
            public GroupInstance Instance { get; }

            internal GroupHandle(Action<GroupInstance> disposeProc, GroupInstance groupInstance, TKey key) : base(disposeProc, groupInstance)
            {
                this.Instance = groupInstance;
                this.Context = this.Instance.Context;
                this.Key = key;
            }
        }

        public class GroupInstance
        {
            public TKey Key;
            public TGroupContext Context;
            public int Num;
        }

        public delegate TGroupContext NewGroupContextCallback(TKey key, object userState);
        public delegate void DeleteGroupContextCallback(TKey key, TGroupContext groupContext, object userState);

        public object UserState { get; }

        NewGroupContextCallback NewGroupContextProc;
        DeleteGroupContextCallback DeleteGroupContextProc;

        Dictionary<TKey, GroupInstance> Hash = new Dictionary<TKey, GroupInstance>();

        CriticalSection LockObj = new CriticalSection();

        public GroupManager(NewGroupContextCallback onNewGroup, DeleteGroupContextCallback onDeleteGroup, object userState = null)
        {
            NewGroupContextProc = onNewGroup;
            DeleteGroupContextProc = onDeleteGroup;
            UserState = userState;
        }

        public GroupHandle Enter(TKey key)
        {
            lock (LockObj)
            {
                GroupInstance g = null;
                if (Hash.TryGetValue(key, out g) == false)
                {
                    var context = NewGroupContextProc(key, UserState);
                    g = new GroupInstance()
                    {
                        Key = key,
                        Context = context,
                        Num = 0,
                    };
                    Hash.Add(key, g);
                }

                Debug.Assert(g.Num >= 0);
                g.Num++;

                return new GroupHandle(x =>
                {
                    lock (LockObj)
                    {
                        x.Num--;
                        Debug.Assert(x.Num >= 0);

                        if (x.Num == 0)
                        {
                            Hash.Remove(x.Key);

                            DeleteGroupContextProc(x.Key, x.Context, this.UserState);
                        }
                    }
                }, g, key);
            }
        }

        public void Dispose() => Dispose(true);
        Once DisposeFlag;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing || DisposeFlag.IsFirstCall() == false) return;
            lock (LockObj)
            {
                foreach (var v in Hash.Values)
                {
                    try
                    {
                        DeleteGroupContextProc(v.Key, v.Context, this.UserState);
                    }
                    catch { }
                }

                Hash.Clear();
            }
        }
    }

    class DelayAction : AsyncCleanupable
    {
        public Action<object> Action { get; }
        public object UserState { get; }
        public int Timeout { get; }

        Task MainTask;

        public bool IsCompleted = false;
        public bool IsCompletedSuccessfully = false;
        public bool IsCanceled = false;

        public Exception Exception { get; private set; } = null;

        CancellationTokenSource CancelSource = new CancellationTokenSource();

        public DelayAction(AsyncCleanuperLady lady, int timeout, Action<object> action, object userState = null)
            : base(lady)
        {
            if (timeout < 0 || timeout == int.MaxValue) timeout = System.Threading.Timeout.Infinite;

            this.Timeout = timeout;
            this.Action = action;
            this.UserState = userState;

            this.MainTask = MainTaskProc().AddToLady(this);
        }

        void InternalInvokeAction()
        {
            try
            {
                this.Action(this.UserState);

                IsCompleted = true;
                IsCompletedSuccessfully = true;
            }
            catch (Exception ex)
            {
                IsCompleted = true;
                IsCompletedSuccessfully = false;

                Exception = ex;
            }
        }

        async Task MainTaskProc()
        {
            using (LeakChecker.Enter())
            {
                try
                {
                    await WebSocketHelper.WaitObjectsAsync(timeout: this.Timeout,
                        cancels: CancelSource.Token.ToSingleArray(),
                        exceptions: ExceptionWhen.CancelException);

                    InternalInvokeAction();
                }
                catch
                {
                    IsCompleted = true;
                    IsCanceled = true;
                    IsCompletedSuccessfully = false;
                }
            }
        }

        public void Cancel() => Dispose();

        Once DisposeFlag;
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!disposing || DisposeFlag.IsFirstCall() == false) return;
                CancelSource.Cancel();
            }
            finally { base.Dispose(disposing); }
        }
    }

    class Datagram
    {
        public Memory<byte> Data;
        public EndPoint EndPoint;
        public byte Flag;

        public IPEndPoint IPEndPoint { get => (IPEndPoint)EndPoint; set => EndPoint = value; }

        public Datagram(Memory<byte> data, EndPoint endPoint, byte flag = 0)
        {
            Data = data;
            EndPoint = endPoint;
            Flag = flag;
        }
    }

    class NonBlockSocket : IDisposable
    {
        public PalSocket Sock { get; }
        public bool IsStream { get; }
        public bool IsDisconnected { get => Watcher.Canceled; }
        public CancellationToken CancelToken { get => Watcher.CancelToken; }

        public AsyncAutoResetEvent EventSendReady { get; } = new AsyncAutoResetEvent();
        public AsyncAutoResetEvent EventRecvReady { get; } = new AsyncAutoResetEvent();
        public AsyncAutoResetEvent EventSendNow { get; } = new AsyncAutoResetEvent();

        CancelWatcher Watcher;
        byte[] TmpRecvBuffer;

        public Fifo RecvTcpFifo { get; } = new Fifo();
        public Fifo SendTcpFifo { get; } = new Fifo();

        public Queue<Datagram> RecvUdpQueue { get; } = new Queue<Datagram>();
        public Queue<Datagram> SendUdpQueue { get; } = new Queue<Datagram>();

        int MaxRecvFifoSize;
        public int MaxRecvUdpQueueSize { get; }

        Task RecvLoopTask = null;
        Task SendLoopTask = null;

        AsyncBulkReceiver<Datagram, int> UdpBulkReader;

        public NonBlockSocket(PalSocket s, CancellationToken cancel = default, int tmpBufferSize = 65536, int maxRecvBufferSize = 65536, int maxRecvUdpQueueSize = 4096)
        {
            if (tmpBufferSize < 65536) tmpBufferSize = 65536;
            TmpRecvBuffer = new byte[tmpBufferSize];
            MaxRecvFifoSize = maxRecvBufferSize;
            MaxRecvUdpQueueSize = maxRecvUdpQueueSize;

            EventSendReady.Set();
            EventRecvReady.Set();

            Sock = s;
            IsStream = (s.SocketType == SocketType.Stream);
            Watcher = new CancelWatcher(cancel);

            if (IsStream)
            {
                RecvLoopTask = TCP_RecvLoop();
                SendLoopTask = TCP_SendLoop();
            }
            else
            {
                UdpBulkReader = new AsyncBulkReceiver<Datagram, int>(async x =>
                {
                    PalSocketReceiveFromResult ret = await Sock.ReceiveFromAsync(TmpRecvBuffer);
                    return new ValueOrClosed<Datagram>(new Datagram(TmpRecvBuffer.AsSpan().Slice(0, ret.ReceivedBytes).ToArray(), (IPEndPoint)ret.RemoteEndPoint));
                });

                RecvLoopTask = UDP_RecvLoop();
                SendLoopTask = UDP_SendLoop();
            }
        }

        async Task TCP_RecvLoop()
        {
            try
            {
                await WebSocketHelper.DoAsyncWithTimeout(async (cancel) =>
                {
                    while (cancel.IsCancellationRequested == false)
                    {
                        int r = await Sock.ReceiveAsync(TmpRecvBuffer);
                        if (r <= 0) break;

                        while (cancel.IsCancellationRequested == false)
                        {
                            lock (RecvTcpFifo)
                            {
                                if (RecvTcpFifo.Size <= MaxRecvFifoSize)
                                {
                                    RecvTcpFifo.Write(TmpRecvBuffer, r);
                                    break;
                                }
                            }

                            await WebSocketHelper.WaitObjectsAsync(cancels: new CancellationToken[] { cancel },
                                timeout: 10);
                        }

                        EventRecvReady.Set();
                    }

                    return 0;
                },
                cancel: Watcher.CancelToken
                );
            }
            finally
            {
                this.Watcher.Cancel();
                EventSendReady.Set();
                EventRecvReady.Set();
            }
        }

        async Task UDP_RecvLoop()
        {
            try
            {
                await WebSocketHelper.DoAsyncWithTimeout(async (cancel) =>
                {
                    while (cancel.IsCancellationRequested == false)
                    {
                        Datagram[] recvPackets = await UdpBulkReader.Recv(cancel);

                        bool fullQueue = false;
                        bool pktReceived = false;

                        lock (RecvUdpQueue)
                        {
                            foreach (Datagram p in recvPackets)
                            {
                                if (RecvUdpQueue.Count <= MaxRecvUdpQueueSize)
                                {
                                    RecvUdpQueue.Enqueue(p);
                                    pktReceived = true;
                                }
                                else
                                {
                                    fullQueue = true;
                                    break;
                                }
                            }
                        }

                        if (fullQueue)
                        {
                            await WebSocketHelper.WaitObjectsAsync(cancels: new CancellationToken[] { cancel },
                                timeout: 10);
                        }

                        if (pktReceived)
                        {
                            EventRecvReady.Set();
                        }
                    }

                    return 0;
                },
                cancel: Watcher.CancelToken
                );
            }
            finally
            {
                this.Watcher.Cancel();
                EventSendReady.Set();
                EventRecvReady.Set();
            }
        }

        async Task TCP_SendLoop()
        {
            try
            {
                await WebSocketHelper.DoAsyncWithTimeout(async (cancel) =>
                {
                    while (cancel.IsCancellationRequested == false)
                    {
                        byte[] sendData = null;

                        while (cancel.IsCancellationRequested == false)
                        {
                            lock (SendTcpFifo)
                            {
                                sendData = SendTcpFifo.Read();
                            }

                            if (sendData != null && sendData.Length >= 1)
                            {
                                break;
                            }

                            await WebSocketHelper.WaitObjectsAsync(cancels: new CancellationToken[] { cancel },
                                events: new AsyncAutoResetEvent[] { EventSendNow });
                        }

                        int r = await Sock.SendAsync(sendData);
                        if (r <= 0) break;

                        EventSendReady.Set();
                    }

                    return 0;
                },
                cancel: Watcher.CancelToken
                );
            }
            finally
            {
                this.Watcher.Cancel();
                EventSendReady.Set();
                EventRecvReady.Set();
            }
        }

        async Task UDP_SendLoop()
        {
            try
            {
                await WebSocketHelper.DoAsyncWithTimeout(async (cancel) =>
                {
                    while (cancel.IsCancellationRequested == false)
                    {
                        Datagram pkt = null;

                        while (cancel.IsCancellationRequested == false)
                        {
                            lock (SendUdpQueue)
                            {
                                if (SendUdpQueue.Count >= 1)
                                {
                                    pkt = SendUdpQueue.Dequeue();
                                }
                            }

                            if (pkt != null)
                            {
                                break;
                            }

                            await WebSocketHelper.WaitObjectsAsync(cancels: new CancellationToken[] { cancel },
                                events: new AsyncAutoResetEvent[] { EventSendNow });
                        }

                        int r = await Sock.SendToAsync(pkt.Data.AsSegment(), pkt.IPEndPoint);
                        if (r <= 0) break;

                        EventSendReady.Set();
                    }

                    return 0;
                },
                cancel: Watcher.CancelToken
                );
            }
            catch (Exception ex)
            {
                Dbg.Where(ex);
            }
            finally
            {
                this.Watcher.Cancel();
                EventSendReady.Set();
                EventRecvReady.Set();
            }
        }

        public Datagram RecvFromNonBlock()
        {
            if (IsDisconnected) return null;
            lock (RecvUdpQueue)
            {
                if (RecvUdpQueue.TryDequeue(out Datagram ret))
                {
                    return ret;
                }
            }
            return null;
        }

        public void Dispose() => Dispose(true);
        Once DisposeFlag;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing || DisposeFlag.IsFirstCall() == false) return;
            Watcher.DisposeSafe();
            Sock.DisposeSafe();
        }
    }

    struct ValueOrClosed<T>
    {
        bool InternalIsOpen;
        public T Value;

        public bool IsOpen { get => InternalIsOpen; }
        public bool IsClosed { get => !InternalIsOpen; }

        public ValueOrClosed(T value)
        {
            InternalIsOpen = true;
            Value = value;
        }
    }

    class AsyncBulkReceiver<TUserReturnElement, TUserState>
    {
        public delegate Task<ValueOrClosed<TUserReturnElement>> AsyncReceiveCallback(TUserState state);

        public int DefaultMaxCount { get; } = 1024;

        AsyncReceiveCallback AsyncReceiveProc;

        public AsyncBulkReceiver(AsyncReceiveCallback asyncReceiveProc, int defaultMaxCount = 1024)
        {
            DefaultMaxCount = defaultMaxCount;
            AsyncReceiveProc = asyncReceiveProc;
        }

        Task<ValueOrClosed<TUserReturnElement>> pushedUserTask = null;

        public async Task<TUserReturnElement[]> Recv(CancellationToken cancel, TUserState state = default(TUserState), int? maxCount = null)
        {
            if (maxCount == null) maxCount = DefaultMaxCount;
            if (maxCount <= 0) maxCount = int.MaxValue;
            List<TUserReturnElement> ret = new List<TUserReturnElement>();

            while (true)
            {
                cancel.ThrowIfCancellationRequested();

                Task<ValueOrClosed<TUserReturnElement>> userTask;
                if (pushedUserTask != null)
                {
                    userTask = pushedUserTask;
                    pushedUserTask = null;
                }
                else
                {
                    userTask = AsyncReceiveProc(state);
                }
                if (userTask.IsCompleted == false)
                {
                    if (ret.Count >= 1)
                    {
                        pushedUserTask = userTask;
                        break;
                    }
                    else
                    {
                        await WebSocketHelper.WaitObjectsAsync(
                            tasks: new Task[] { userTask },
                            cancels: new CancellationToken[] { cancel });

                        cancel.ThrowIfCancellationRequested();

                        if (userTask.Result.IsOpen)
                        {
                            ret.Add(userTask.Result.Value);
                        }
                        else
                        {
                            pushedUserTask = userTask;
                            break;
                        }
                    }
                }
                else
                {
                    if (userTask.Result.IsOpen)
                    {
                        ret.Add(userTask.Result.Value);
                    }
                    else
                    {
                        break;
                    }
                }
                if (ret.Count >= maxCount) break;
            }

            if (ret.Count >= 1)
                return ret.ToArray();
            else
                return null; // Disconnected
        }
    }

    class ExceptionQueue
    {
        public const int MaxItems = 128;
        SharedQueue<Exception> Queue = new SharedQueue<Exception>(MaxItems, true);
        public AsyncManualResetEvent WhenExceptionAdded { get; } = new AsyncManualResetEvent();

        HashSet<Task> WatchedTasks = new HashSet<Task>();

        public void Raise(Exception ex) => Add(ex, true);

        public void Add(Exception ex, bool raiseFirstException = false, bool doNotCheckWatchedTasks = false)
        {
            if (ex == null)
                ex = new Exception("null exception");

            if (doNotCheckWatchedTasks == false)
                CheckWatchedTasks();

            Exception throwingException = null;

            AggregateException aex = ex as AggregateException;

            if (aex != null)
            {
                var exp = aex.Flatten().InnerExceptions;

                lock (SharedQueue<Exception>.GlobalLock)
                {
                    foreach (var expi in exp)
                        Queue.Enqueue(expi);

                    if (raiseFirstException)
                        throwingException = Queue.ItemsReadOnly[0];
                }
            }
            else
            {
                lock (SharedQueue<Exception>.GlobalLock)
                {
                    Queue.Enqueue(ex);
                    if (raiseFirstException)
                        throwingException = Queue.ItemsReadOnly[0];
                }
            }

            WhenExceptionAdded.Set(true);

            if (throwingException != null)
                throwingException.ReThrow();
        }

        public void Encounter(ExceptionQueue other) => this.Queue.Encounter(other.Queue);

        public Exception[] GetExceptions()
        {
            CheckWatchedTasks();
            return this.Queue.ItemsReadOnly;
        }
        public Exception[] Exceptions => GetExceptions();
        public Exception FirstException => Exceptions.FirstOrDefault();

        public void ThrowFirstExceptionIfExists()
        {
            Exception ex = null;
            lock (SharedQueue<Exception>.GlobalLock)
            {
                if (HasError)
                    ex = FirstException;
            }

            if (ex != null)
                ex.ReThrow();
        }

        public bool HasError => Exceptions.Length != 0;
        public bool IsOk => !HasError;

        public bool RegisterWatchedTask(Task t)
        {
            if (t.IsCompleted)
            {
                if (t.IsFaulted)
                    Add(t.Exception);
                else if (t.IsCanceled)
                    Add(new TaskCanceledException());

                return true;
            }

            bool ret;

            lock (SharedQueue<Exception>.GlobalLock)
            {
                ret = WatchedTasks.Add(t);
            }

            t.ContinueWith(x =>
            {
                CheckWatchedTasks();
            });

            return ret;
        }

        public bool UnregisterWatchedTask(Task t)
        {
            lock (SharedQueue<Exception>.GlobalLock)
                return WatchedTasks.Remove(t);
        }

        void CheckWatchedTasks()
        {
            List<Task> o = new List<Task>();

            List<Exception> expList = new List<Exception>();

            lock (SharedQueue<Exception>.GlobalLock)
            {
                foreach (Task t in WatchedTasks)
                {
                    if (t.IsCompleted)
                    {
                        if (t.IsFaulted)
                            expList.Add(t.Exception);
                        else if (t.IsCanceled)
                            expList.Add(new TaskCanceledException());

                        o.Add(t);
                    }
                }

                foreach (Task t in o)
                    WatchedTasks.Remove(t);
            }

            foreach (Exception ex in expList)
                Add(ex, doNotCheckWatchedTasks: true);
        }
    }

    class HierarchyPosition : RefInt
    {
        public HierarchyPosition() : base(-1) { }
        public bool IsInstalled { get => (this.Value != -1); }
    }

    class SharedHierarchy<T>
    {
        public class HierarchyBodyItem : IComparable<HierarchyBodyItem>, IEquatable<HierarchyBodyItem>
        {
            public HierarchyPosition Position;
            public T Value;
            public HierarchyBodyItem(HierarchyPosition position, T value)
            {
                this.Position = position;
                this.Value = value;
            }

            public int CompareTo(HierarchyBodyItem other) => this.Position.CompareTo(other.Position);
            public bool Equals(HierarchyBodyItem other) => this.Position.Equals(other.Position);
            public override bool Equals(object obj) => (obj is HierarchyBodyItem) ? this.Position.Equals(obj as HierarchyBodyItem) : false;
            public override int GetHashCode() => this.Position.GetHashCode();
        }

        class HierarchyBody
        {
            public List<HierarchyBodyItem> _InternalList = new List<HierarchyBodyItem>();

            public HierarchyBody Next = null;

            public HierarchyBody() { }

            public HierarchyBodyItem[] ToArray()
            {
                lock (_InternalList)
                    return _InternalList.ToArray();
            }

            public HierarchyBodyItem Join(HierarchyPosition targetPosition, bool joinAsSuperior, T value, HierarchyPosition myPosition)
            {
                lock (_InternalList)
                {
                    if (targetPosition == null)
                    {
                        var me = new HierarchyBodyItem(myPosition, value);
                        var current = _InternalList;

                        current = current.Append(me).ToList();

                        int positionIncrement = 0;
                        current.ForEach(x => x.Position.Set(++positionIncrement));

                        _InternalList.Clear();
                        _InternalList.AddRange(current);

                        return me;
                    }
                    else
                    {
                        var current = _InternalList;

                        var inferiors = current.Where(x => joinAsSuperior ? (x.Position <= targetPosition) : (x.Position < targetPosition));
                        var me = new HierarchyBodyItem(myPosition, value);
                        var superiors = current.Where(x => joinAsSuperior ? (x.Position > targetPosition) : (x.Position >= targetPosition));

                        current = inferiors.Append(me).Concat(superiors).ToList();

                        int positionIncrement = 0;
                        current.ForEach(x => x.Position.Set(++positionIncrement));

                        _InternalList.Clear();
                        _InternalList.AddRange(current);

                        return me;
                    }
                }
            }

            public void Resign(HierarchyBodyItem me)
            {
                lock (_InternalList)
                {
                    var current = _InternalList;

                    if (current.Contains(me))
                    {
                        current.Remove(me);

                        int positionIncrement = 0;
                        current.ForEach(x => x.Position.Set(++positionIncrement));

                        Debug.Assert(me.Position.IsInstalled);

                        me.Position.Set(-1);
                    }
                    else
                    {
                        Debug.Assert(false);
                    }
                }
            }

            public static void Merge(HierarchyBody inferiors, HierarchyBody superiors)
            {
                if (inferiors == superiors) return;

                Debug.Assert(inferiors._InternalList != null);
                Debug.Assert(superiors._InternalList != null);

                lock (inferiors._InternalList)
                {
                    lock (superiors._InternalList)
                    {
                        HierarchyBody merged = new HierarchyBody();
                        merged._InternalList.AddRange(inferiors._InternalList.Concat(superiors._InternalList));

                        int positionIncrement = 0;
                        merged._InternalList.ForEach(x => x.Position.Set(++positionIncrement));

                        inferiors._InternalList = superiors._InternalList = null;
                        Debug.Assert(inferiors.Next == null); Debug.Assert(superiors.Next == null);
                        inferiors.Next = superiors.Next = merged;
                    }
                }
            }

            public HierarchyBody GetLast()
            {
                if (Next == null)
                    return this;
                else
                    return Next.GetLast();
            }
        }

        HierarchyBody First;

        public static readonly object GlobalLock = new object();

        public SharedHierarchy()
        {
            First = new HierarchyBody();
        }

        public void Encounter(SharedHierarchy<T> inferiors)
        {
            if (this == inferiors) return;

            lock (GlobalLock)
            {
                HierarchyBody inferiorsBody = inferiors.First.GetLast();
                HierarchyBody superiorsBody = this.First.GetLast();
                if (inferiorsBody == superiorsBody) return;

                HierarchyBody.Merge(inferiorsBody, superiorsBody);
            }
        }

        public HierarchyBodyItem Join(HierarchyPosition targetPosition, bool joinAsSuperior, T value, HierarchyPosition myPosition)
        {
            Debug.Assert(myPosition.IsInstalled == false);

            lock (GlobalLock)
                return this.First.GetLast().Join(targetPosition, joinAsSuperior, value, myPosition);
        }

        public void Resign(HierarchyBodyItem me)
        {
            Debug.Assert(me.Position.IsInstalled);

            lock (GlobalLock)
                this.First.GetLast().Resign(me);
        }

        public HierarchyBodyItem[] ToArrayWithPositions()
        {
            lock (GlobalLock)
                return this.First.GetLast().ToArray();
        }

        public HierarchyBodyItem[] ItemsWithPositionsReadOnly { get => ToArrayWithPositions(); }

        public T[] ToArray() => ToArrayWithPositions().Select(x => x.Value).ToArray();
        public T[] ItemsReadOnly { get => ToArray(); }
    }

    abstract class LayerInfoBase
    {
        public HierarchyPosition Position { get; } = new HierarchyPosition();
        internal SharedHierarchy<LayerInfoBase>.HierarchyBodyItem _InternalHierarchyBodyItem = null;
        internal LayerInfo _InternalLayerStack = null;

        public FastStackBase ProtocolStack { get; private set; } = null;

        public bool IsInstalled => Position.IsInstalled;

        public void Install(LayerInfo info, LayerInfoBase targetLayer, bool joinAsSuperior)
            => info.Install(this, targetLayer, joinAsSuperior);

        public void Uninstall()
            => _InternalLayerStack.Uninstall(this);

        internal void _InternalSetProtocolStack(FastStackBase protocolStack)
            => ProtocolStack = protocolStack;
    }

    interface ILayerInfoSsl
    {
        bool IsServerMode { get; }
        string SslProtocol { get; }
        string CipherAlgorithm { get; }
        int CipherStrength { get; }
        string HashAlgorithm { get; }
        int HashStrength { get; }
        string KeyExchangeAlgorithm { get; }
        int KeyExchangeStrength { get; }
        X509Certificate LocalCertificate { get; }
        X509Certificate RemoteCertificate { get; }
    }

    interface ILayerInfoIpEndPoint
    {
        IPAddress LocalIPAddress { get; }
        IPAddress RemoteIPAddress { get; }
    }

    interface ILayerInfoTcpEndPoint : ILayerInfoIpEndPoint
    {
        int LocalPort { get; }
        int RemotePort { get; }
    }

    class LayerInfo
    {
        SharedHierarchy<LayerInfoBase> Hierarchy = new SharedHierarchy<LayerInfoBase>();

        public void Install(LayerInfoBase info, LayerInfoBase targetLayer, bool joinAsSuperior)
        {
            Debug.Assert(info.IsInstalled == false); Debug.Assert(info._InternalHierarchyBodyItem == null); Debug.Assert(info._InternalLayerStack == null);

            info._InternalHierarchyBodyItem = Hierarchy.Join(targetLayer?.Position, joinAsSuperior, info, info.Position);
            info._InternalLayerStack = this;

            Debug.Assert(info.IsInstalled); Debug.Assert(info._InternalHierarchyBodyItem != null);
        }

        public void Uninstall(LayerInfoBase info)
        {
            Debug.Assert(info.IsInstalled); Debug.Assert(info._InternalHierarchyBodyItem != null); Debug.Assert(info._InternalLayerStack != null);

            Hierarchy.Resign(info._InternalHierarchyBodyItem);

            info._InternalHierarchyBodyItem = null;
            info._InternalLayerStack = null;

            Debug.Assert(info.IsInstalled == false);
        }

        public T[] GetValues<T>() where T : class
        {
            var items = Hierarchy.ItemsWithPositionsReadOnly;

            return items.Select(x => x.Value as T).Where(x => x != null).ToArray();
        }

        public T GetValue<T>(int index = 0) where T : class
        {
            return GetValues<T>()[index];
        }

        public void Encounter(LayerInfo other) => this.Hierarchy.Encounter(other.Hierarchy);

        public ILayerInfoSsl Ssl => GetValue<ILayerInfoSsl>();
        public ILayerInfoIpEndPoint Ip => GetValue<ILayerInfoIpEndPoint>();
        public ILayerInfoTcpEndPoint Tcp => GetValue<ILayerInfoTcpEndPoint>();
    }

    class SharedQueue<T>
    {
        class QueueBody
        {
            static long globalTimestamp;

            public QueueBody Next;

            public SortedList<long, T> _InternalList = new SortedList<long, T>();
            public readonly int MaxItems;

            public QueueBody(int maxItems)
            {
                if (maxItems <= 0) maxItems = int.MaxValue;
                MaxItems = maxItems;
            }

            public void Enqueue(T item, bool distinct = false)
            {
                lock (_InternalList)
                {
                    if (_InternalList.Count > MaxItems) return;
                    if (distinct && _InternalList.ContainsValue(item)) return;
                    long ts = Interlocked.Increment(ref globalTimestamp);
                    _InternalList.Add(ts, item);
                }
            }

            public T Dequeue()
            {
                lock (_InternalList)
                {
                    if (_InternalList.Count == 0) return default(T);
                    long ts = _InternalList.Keys[0];
                    T ret = _InternalList[ts];
                    _InternalList.Remove(ts);
                    return ret;
                }
            }

            public T[] ToArray()
            {
                lock (_InternalList)
                {
                    return _InternalList.Values.ToArray();
                }
            }

            public static void Merge(QueueBody q1, QueueBody q2)
            {
                if (q1 == q2) return;
                Debug.Assert(q1._InternalList != null);
                Debug.Assert(q2._InternalList != null);

                lock (q1._InternalList)
                {
                    lock (q2._InternalList)
                    {
                        QueueBody q3 = new QueueBody(Math.Max(q1.MaxItems, q2.MaxItems));
                        foreach (long ts in q1._InternalList.Keys)
                            q3._InternalList.Add(ts, q1._InternalList[ts]);
                        foreach (long ts in q2._InternalList.Keys)
                            q3._InternalList.Add(ts, q2._InternalList[ts]);
                        if (q3._InternalList.Count > q3.MaxItems)
                        {
                            int num = 0;
                            List<long> removeList = new List<long>();
                            foreach (long ts in q3._InternalList.Keys)
                            {
                                num++;
                                if (num > q3.MaxItems)
                                    removeList.Add(ts);
                            }
                            foreach (long ts in removeList)
                                q3._InternalList.Remove(ts);
                        }
                        q1._InternalList = null;
                        q2._InternalList = null;
                        Debug.Assert(q1.Next == null);
                        Debug.Assert(q2.Next == null);
                        q1.Next = q3;
                        q2.Next = q3;
                    }
                }
            }

            public QueueBody GetLast()
            {
                if (Next == null)
                    return this;
                else
                    return Next.GetLast();
            }
        }

        QueueBody First;

        public static readonly object GlobalLock = new object();

        public bool Distinct { get; }

        public SharedQueue(int maxItems = 0, bool distinct = false)
        {
            Distinct = distinct;
            First = new QueueBody(maxItems);
        }

        public void Encounter(SharedQueue<T> other)
        {
            if (this == other) return;

            lock (GlobalLock)
            {
                QueueBody last1 = this.First.GetLast();
                QueueBody last2 = other.First.GetLast();
                if (last1 == last2) return;

                QueueBody.Merge(last1, last2);
            }
        }

        public void Enqueue(T value)
        {
            lock (GlobalLock)
                this.First.GetLast().Enqueue(value, Distinct);
        }

        public T Dequeue()
        {
            lock (GlobalLock)
                return this.First.GetLast().Dequeue();
        }

        public T[] ToArray()
        {
            lock (GlobalLock)
                return this.First.GetLast().ToArray();
        }

        public int CountFast
        {
            get
            {
                var q = this.First.GetLast();
                var list = q._InternalList;
                if (list == null) return 0;
                lock (list)
                    return list.Count;
            }
        }

        public T[] ItemsReadOnly { get => ToArray(); }
    }

    class FastLinkedListNode<T>
    {
        public T Value;
        public FastLinkedListNode<T> Next, Previous;
    }

    class FastLinkedList<T>
    {
        public int Count;
        public FastLinkedListNode<T> First, Last;

        public void Clear()
        {
            Count = 0;
            First = Last = null;
        }

        public FastLinkedListNode<T> AddFirst(T value)
        {
            if (First == null)
            {
                Debug.Assert(Last == null);
                Debug.Assert(Count == 0);
                First = Last = new FastLinkedListNode<T>() { Value = value, Next = null, Previous = null };
                Count++;
                return First;
            }
            else
            {
                Debug.Assert(Last != null);
                Debug.Assert(Count >= 1);
                var oldFirst = First;
                var nn = new FastLinkedListNode<T>() { Value = value, Next = oldFirst, Previous = null };
                Debug.Assert(oldFirst.Previous == null);
                oldFirst.Previous = nn;
                First = nn;
                Count++;
                return nn;
            }
        }

        public void AddFirst(FastLinkedListNode<T> chainFirst, FastLinkedListNode<T> chainLast, int chainedCount)
        {
            if (First == null)
            {
                Debug.Assert(Last == null);
                Debug.Assert(Count == 0);
                First = chainFirst;
                Last = chainLast;
                chainFirst.Previous = null;
                chainLast.Next = null;
                Count = chainedCount;
            }
            else
            {
                Debug.Assert(Last != null);
                Debug.Assert(Count >= 1);
                var oldFirst = First;
                Debug.Assert(oldFirst.Previous == null);
                oldFirst.Previous = chainLast;
                First = chainFirst;
                Count += chainedCount;
            }
        }

        public FastLinkedListNode<T> AddLast(T value)
        {
            if (Last == null)
            {
                Debug.Assert(First == null);
                Debug.Assert(Count == 0);
                First = Last = new FastLinkedListNode<T>() { Value = value, Next = null, Previous = null };
                Count++;
                return Last;
            }
            else
            {
                Debug.Assert(First != null);
                Debug.Assert(Count >= 1);
                var oldLast = Last;
                var nn = new FastLinkedListNode<T>() { Value = value, Next = null, Previous = oldLast };
                Debug.Assert(oldLast.Next == null);
                oldLast.Next = nn;
                Last = nn;
                Count++;
                return nn;
            }
        }

        public void AddLast(FastLinkedListNode<T> chainFirst, FastLinkedListNode<T> chainLast, int chainedCount)
        {
            if (Last == null)
            {
                Debug.Assert(First == null);
                Debug.Assert(Count == 0);
                First = chainFirst;
                Last = chainLast;
                chainFirst.Previous = null;
                chainLast.Next = null;
                Count = chainedCount;
            }
            else
            {
                Debug.Assert(First != null);
                Debug.Assert(Count >= 1);
                var oldLast = Last;
                Debug.Assert(oldLast.Next == null);
                oldLast.Next = chainFirst;
                Last = chainLast;
                Count += chainedCount;
            }
        }

        public FastLinkedListNode<T> AddAfter(FastLinkedListNode<T> prevNode, T value)
        {
            var nextNode = prevNode.Next;
            Debug.Assert(First != null && Last != null);
            Debug.Assert(nextNode != null || Last == prevNode);
            Debug.Assert(nextNode == null || nextNode.Previous == prevNode);
            var nn = new FastLinkedListNode<T>() { Value = value, Next = nextNode, Previous = prevNode };
            prevNode.Next = nn;
            if (nextNode != null) nextNode.Previous = nn;
            if (Last == prevNode) Last = nn;
            Count++;
            return nn;
        }

        public void AddAfter(FastLinkedListNode<T> prevNode, FastLinkedListNode<T> chainFirst, FastLinkedListNode<T> chainLast, int chainedCount)
        {
            var nextNode = prevNode.Next;
            Debug.Assert(First != null && Last != null);
            Debug.Assert(nextNode != null || Last == prevNode);
            Debug.Assert(nextNode == null || nextNode.Previous == prevNode);
            prevNode.Next = chainFirst;
            chainFirst.Previous = prevNode;
            if (nextNode != null) nextNode.Previous = chainLast;
            chainLast.Previous = nextNode;
            if (Last == prevNode) Last = chainLast;
            Count += chainedCount;
        }

        public FastLinkedListNode<T> AddBefore(FastLinkedListNode<T> nextNode, T value)
        {
            var prevNode = nextNode.Previous;
            Debug.Assert(First != null && Last != null);
            Debug.Assert(prevNode != null || First == nextNode);
            Debug.Assert(prevNode == null || prevNode.Next == nextNode);
            var nn = new FastLinkedListNode<T>() { Value = value, Next = nextNode, Previous = prevNode };
            nextNode.Previous = nn;
            if (prevNode != null) prevNode.Next = nn;
            if (First == nextNode) First = nn;
            Count++;
            return nn;
        }

        public void AddBefore(FastLinkedListNode<T> nextNode, FastLinkedListNode<T> chainFirst, FastLinkedListNode<T> chainLast, int chainedCount)
        {
            var prevNode = nextNode.Previous;
            Debug.Assert(First != null && Last != null);
            Debug.Assert(prevNode != null || First == nextNode);
            Debug.Assert(prevNode == null || prevNode.Next == nextNode);
            nextNode.Previous = chainLast;
            chainLast.Next = nextNode;
            if (prevNode != null) prevNode.Next = chainFirst;
            chainFirst.Previous = prevNode;
            if (First == nextNode) First = chainFirst;
            Count += chainedCount;
        }

        public void Remove(FastLinkedListNode<T> node)
        {
            Debug.Assert(First != null && Last != null);

            if (node.Previous != null && node.Next != null)
            {
                Debug.Assert(First != null);
                Debug.Assert(Last != null);
                Debug.Assert(First != node);
                Debug.Assert(Last != node);

                node.Previous.Next = node.Next;
                node.Next.Previous = node.Previous;

                Count--;
            }
            else if (node.Previous == null && node.Next == null)
            {
                Debug.Assert(First == node);
                Debug.Assert(Last == node);

                First = Last = null;

                Count--;
            }
            else if (node.Previous != null)
            {
                Debug.Assert(First != null);
                Debug.Assert(First != node);
                Debug.Assert(Last == node);

                node.Previous.Next = null;
                Last = node.Previous;

                Count--;
            }
            else
            {
                Debug.Assert(Last != null);
                Debug.Assert(Last != node);
                Debug.Assert(First == node);

                node.Next.Previous = null;
                First = node.Next;

                Count--;
            }
        }

        public T[] ItemsReadOnly
        {
            get
            {
                List<T> ret = new List<T>();
                var node = First;
                while (node != null)
                {
                    ret.Add(node.Value);
                    node = node.Next;
                }
                return ret.ToArray();
            }
        }
    }

    class LocalTimer
    {
        SortedSet<long> List = new SortedSet<long>();
        HashSet<long> Hash = new HashSet<long>();
        public long Now { get; private set; } = FastTick64.Now;
        public bool AutomaticUpdateNow { get; }

        public LocalTimer(bool automaticUpdateNow = true)
        {
            AutomaticUpdateNow = automaticUpdateNow;
        }

        public void UpdateNow() => Now = FastTick64.Now;
        public void UpdateNow(long nowTick) => Now = nowTick;

        public long AddTick(long tick)
        {
            if (Hash.Add(tick))
                List.Add(tick);

            return tick;
        }

        public long AddTimeout(int interval)
        {
            if (interval == Timeout.Infinite) return long.MaxValue;
            interval = Math.Max(interval, 0);
            if (AutomaticUpdateNow) UpdateNow();
            long v = Now + interval;
            AddTick(v);
            return v;
        }

        public int GetNextInterval()
        {
            int ret = Timeout.Infinite;
            if (AutomaticUpdateNow) UpdateNow();
            long now = Now;
            List<long> deleteList = null;

            foreach (long v in List)
            {
                if (now >= v)
                {
                    ret = 0;
                    if (deleteList == null) deleteList = new List<long>();
                    deleteList.Add(v);
                }
                else
                {
                    break;
                }
            }

            if (deleteList != null)
            {
                foreach (long v in deleteList)
                {
                    List.Remove(v);
                    Hash.Remove(v);
                }
            }

            if (ret == Timeout.Infinite)
            {
                if (List.Count >= 1)
                {
                    long v = List.First();
                    ret = (int)(v - now);
                    Debug.Assert(ret > 0);
                    if (ret <= 0) ret = 0;
                }
            }

            return ret;
        }
    }

    public readonly struct BackgroundStateDataUpdatePolicy
    {
        public readonly int InitialPollingInterval;
        public readonly int MaxPollingInterval;
        public readonly int IdleTimeoutToFreeThreadInterval;

        public const int DefaultInitialPollingInterval = 1 * 1000;
        public const int DefaultMaxPollingInterval = 60 * 1000;
        public const int DefaultIdleTimeoutToFreeThreadInterval = 180 * 1000;

        public BackgroundStateDataUpdatePolicy(int initialPollingInterval = DefaultInitialPollingInterval,
            int maxPollingInterval = DefaultMaxPollingInterval,
            int timeoutToStopThread = DefaultIdleTimeoutToFreeThreadInterval)
        {
            InitialPollingInterval = initialPollingInterval;
            MaxPollingInterval = maxPollingInterval;
            IdleTimeoutToFreeThreadInterval = timeoutToStopThread;
        }

        public static BackgroundStateDataUpdatePolicy Default { get; }
            = new BackgroundStateDataUpdatePolicy(1 * 1000, 60 * 1000, 30 * 1000);

        public BackgroundStateDataUpdatePolicy SafeValue
        {
            get
            {
                return new BackgroundStateDataUpdatePolicy(
                    Math.Max(this.InitialPollingInterval, 1 * 100),
                    Math.Max(this.MaxPollingInterval, 1 * 500),
                    Math.Max(Math.Max(this.IdleTimeoutToFreeThreadInterval, 1 * 500), this.MaxPollingInterval)
                    );
            }
        }
    }

    abstract class BackgroundStateDataBase : IEquatable<BackgroundStateDataBase>
    {
        public DateTimeOffset TimeStamp { get; } = DateTimeOffset.Now;
        public long TickTimeStamp { get; } = FastTick64.Now;

        public abstract BackgroundStateDataUpdatePolicy DataUpdatePolicy { get; }

        public abstract bool Equals(BackgroundStateDataBase other);

        public abstract void RegisterSystemStateChangeNotificationCallbackOnlyOnce(Action callMe);
    }

    static class BackgroundState<TData>
        where TData : BackgroundStateDataBase, new()
    {
        public struct CurrentData
        {
            public int Version;
            public TData Data;
        }

        public static CurrentData Current
        {
            get
            {
                CurrentData d = new CurrentData();
                d.Data = GetState();
                d.Version = InternalVersion;
                return d;
            }
        }

        static volatile TData CacheData = null;

        static volatile int NumRead = 0;

        static volatile int InternalVersion = 0;

        static bool CallbackIsRegistered = false;

        static CriticalSection LockObj = new CriticalSection();
        static Thread thread = null;
        static AutoResetEvent threadSignal = new AutoResetEvent(false);
        static bool callbackIsCalled = false;

        public static FastEventListenerList<TData, int> EventListener { get; } = new FastEventListenerList<TData, int>();

        static TData TryGetTData()
        {
            try
            {
                TData ret = new TData();

                if (CallbackIsRegistered == false)
                {
                    try
                    {
                        ret.RegisterSystemStateChangeNotificationCallbackOnlyOnce(() =>
                        {
                            callbackIsCalled = true;
                            GetState();
                            threadSignal.Set();
                        });

                        CallbackIsRegistered = true;
                    }
                    catch { }
                }

                return ret;
            }
            catch
            {
                return null;
            }
        }

        static TData GetState()
        {
            NumRead++;

            if (CacheData != null)
            {
                if (thread == null)
                {
                    EnsureStartThreadIfStopped(CacheData.DataUpdatePolicy);
                }

                return CacheData;
            }
            else
            {
                BackgroundStateDataUpdatePolicy updatePolicy = BackgroundStateDataUpdatePolicy.Default;
                TData data = TryGetTData();
                if (data != null)
                {
                    updatePolicy = data.DataUpdatePolicy;

                    bool inc = false;
                    if (CacheData == null)
                    {
                        inc = true;
                    }
                    else
                    {
                        if (CacheData.Equals(data) == false)
                            inc = true;
                    }
                    CacheData = data;

                    if (inc)
                    {
                        InternalVersion++;
                        EventListener.Fire(CacheData, 0);
                    }
                }

                EnsureStartThreadIfStopped(updatePolicy);

                return CacheData;
            }
        }

        static void EnsureStartThreadIfStopped(BackgroundStateDataUpdatePolicy updatePolicy)
        {
            lock (LockObj)
            {
                if (thread == null)
                {
                    thread = new Thread(MaintainThread);
                    thread.IsBackground = true;
                    thread.Priority = ThreadPriority.BelowNormal;
                    thread.Name = $"MaintainThread for BackgroundState<{typeof(TData).ToString()}>";
                    thread.Start(updatePolicy);
                }
            }
        }

        static int nextInterval = 0;

        static void MaintainThread(object param)
        {
            BackgroundStateDataUpdatePolicy policy = (BackgroundStateDataUpdatePolicy)param;
            policy = policy.SafeValue;

            LocalTimer tm = new LocalTimer();

            if (nextInterval == 0)
            {
                nextInterval = policy.InitialPollingInterval;
            }

            long nextGetDataTick = tm.AddTimeout(nextInterval);

            long nextIdleDetectTick = tm.AddTimeout(policy.IdleTimeoutToFreeThreadInterval);

            int lastNumRead = NumRead;

            while (true)
            {
                if (FastTick64.Now >= nextGetDataTick || callbackIsCalled)
                {
                    TData data = TryGetTData();

                    nextInterval = Math.Min(nextInterval + policy.InitialPollingInterval, policy.MaxPollingInterval);
                    bool inc = false;

                    if (data != null)
                    {
                        if (data.Equals(CacheData) == false)
                        {
                            nextInterval = policy.InitialPollingInterval;
                            inc = true;
                        }
                        CacheData = data;
                    }
                    else
                    {
                        nextInterval = policy.InitialPollingInterval;
                    }

                    if (callbackIsCalled)
                    {
                        nextInterval = policy.InitialPollingInterval;
                    }

                    if (inc)
                    {
                        InternalVersion++;
                        EventListener.Fire(CacheData, 0);
                    }

                    nextGetDataTick = tm.AddTimeout(nextInterval);

                    callbackIsCalled = false;
                }

                if (FastTick64.Now >= nextIdleDetectTick)
                {
                    int numread = NumRead;
                    if (lastNumRead != numread)
                    {
                        lastNumRead = numread;
                        nextIdleDetectTick = tm.AddTimeout(policy.IdleTimeoutToFreeThreadInterval);
                    }
                    else
                    {
                        thread = null;
                        return;
                    }
                }

                int i = tm.GetNextInterval();

                i = Math.Max(i, 100);

                threadSignal.WaitOne(i);
            }
        }
    }


    class DisconnectedException : Exception { }
    class FastBufferDisconnectedException : DisconnectedException { }
    class SocketDisconnectedException : DisconnectedException { }
    class BaseStreamDisconnectedException : DisconnectedException { }

    delegate void FastEventCallback<TCaller, TEventType>(TCaller caller, TEventType type, object userState);

    class FastEvent<TCaller, TEventType>
    {
        public FastEventCallback<TCaller, TEventType> Proc { get; }
        public object UserState { get; }

        public FastEvent(FastEventCallback<TCaller, TEventType> proc, object userState)
        {
            this.Proc = proc;
            this.UserState = userState;
        }

        public void CallSafe(TCaller buffer, TEventType type)
        {
            try
            {
                this.Proc(buffer, type, UserState);
            }
            catch { }
        }
    }

    class FastEventListenerList<TCaller, TEventType>
    {
        FastReadList<FastEvent<TCaller, TEventType>> ListenerList;
        FastReadList<AsyncAutoResetEvent> AsyncEventList;

        public int RegisterCallback(FastEventCallback<TCaller, TEventType> proc, object userState = null)
        {
            if (proc == null) return 0;
            return ListenerList.Add(new FastEvent<TCaller, TEventType>(proc, userState));
        }

        public bool UnregisterCallback(int id)
        {
            return ListenerList.Delete(id);
        }

        public Holder<int> RegisterCallbackWithUsing(FastEventCallback<TCaller, TEventType> proc, object userState = null)
            => new Holder<int>(id => UnregisterCallback(id), RegisterCallback(proc, userState));

        public int RegisterAsyncEvent(AsyncAutoResetEvent ev)
        {
            if (ev == null) return 0;
            return AsyncEventList.Add(ev);
        }

        public bool UnregisterAsyncEvent(int id)
        {
            return AsyncEventList.Delete(id);
        }

        public Holder<int> RegisterAsyncEventWithUsing(AsyncAutoResetEvent ev)
            => new Holder<int>(id => UnregisterAsyncEvent(id), RegisterAsyncEvent(ev));

        public void Fire(TCaller caller, TEventType type)
        {
            var listenerList = ListenerList.GetListFast();
            if (listenerList != null)
                foreach (var e in listenerList)
                    e.CallSafe(caller, type);

            var asyncEventList = AsyncEventList.GetListFast();
            if (asyncEventList != null)
                foreach (var e in asyncEventList)
                    e.Set();
        }
    }

    enum FastBufferCallbackEventType
    {
        Init,
        Written,
        Read,
        PartialProcessReadData,
        EmptyToNonEmpty,
        NonEmptyToEmpty,
        Disconnected,
    }

    interface IFastBufferState
    {
        long Id { get; }

        long PinHead { get; }
        long PinTail { get; }
        long Length { get; }

        ExceptionQueue ExceptionQueue { get; }
        LayerInfo Info { get; }

        object LockObj { get; }

        bool IsReadyToWrite { get; }
        bool IsReadyToRead { get; }
        bool IsEventsEnabled { get; }
        AsyncAutoResetEvent EventWriteReady { get; }
        AsyncAutoResetEvent EventReadReady { get; }

        FastEventListenerList<IFastBufferState, FastBufferCallbackEventType> EventListeners { get; }

        void CompleteRead();
        void CompleteWrite(bool checkDisconnect = true);
    }

    interface IFastBuffer<T> : IFastBufferState
    {
        void Clear();
        void Enqueue(T item);
        void EnqueueAll(Span<T> itemList);
        void EnqueueAllWithLock(Span<T> itemList);
        List<T> Dequeue(long minReadSize, out long totalReadSize, bool allowSplitSegments = true);
        List<T> DequeueAll(out long totalReadSize);
        List<T> DequeueAllWithLock(out long totalReadSize);
        long DequeueAllAndEnqueueToOther(IFastBuffer<T> other);
    }

    public readonly struct FastBufferSegment<T>
    {
        public readonly T Item;
        public readonly long Pin;
        public readonly long RelativeOffset;

        public FastBufferSegment(T item, long pin, long relativeOffset)
        {
            Item = item;
            Pin = pin;
            RelativeOffset = relativeOffset;
        }
    }

    class Fifo<T>
    {
        public T[] PhysicalData { get; private set; }
        public int Size { get; private set; }
        public int Position { get; private set; }
        public int PhysicalSize { get => PhysicalData.Length; }

        public const int FifoInitSize = 32;
        public const int FifoReAllocSize = 1024;
        public const int FifoReAllocSizeSmall = 1024;

        public int ReAllocMemSize { get; }

        public Fifo(int reAllocMemSize = FifoReAllocSize)
        {
            ReAllocMemSize = reAllocMemSize;
            Size = Position = 0;
            PhysicalData = new T[FifoInitSize];
        }

        public void Clear()
        {
            Size = Position = 0;
        }

        public void Write(Span<T> data)
        {
            WriteInternal(data, data.Length);
        }

        public void Write(T data)
        {
            WriteInternal(data);
        }

        public void WriteSkip(int length)
        {
            WriteInternal(null, length);
        }

        void WriteInternal(Span<T> src, int size)
        {
            checked
            {
                int oldSize, newSize, needSize;

                oldSize = Size;
                newSize = oldSize + size;
                needSize = Position + newSize;

                bool reallocFlag = false;
                int newPhysicalSize = PhysicalData.Length;
                while (needSize > newPhysicalSize)
                {
                    newPhysicalSize = Math.Max(newPhysicalSize, FifoInitSize) * 3;
                    reallocFlag = true;
                }

                if (reallocFlag)
                    PhysicalData = MemoryHelper.ReAlloc(PhysicalData, newPhysicalSize);

                if (src != null)
                    src.CopyTo(PhysicalData.AsSpan().Slice(Position + oldSize));

                Size = newSize;
            }
        }

        void WriteInternal(T src)
        {
            checked
            {
                int oldSize, newSize, needSize;

                oldSize = Size;
                newSize = oldSize + 1;
                needSize = Position + newSize;

                bool reallocFlag = false;
                int newPhysicalSize = PhysicalData.Length;
                while (needSize > newPhysicalSize)
                {
                    newPhysicalSize = Math.Max(newPhysicalSize, FifoInitSize) * 3;
                    reallocFlag = true;
                }

                if (reallocFlag)
                    PhysicalData = MemoryHelper.ReAlloc(PhysicalData, newPhysicalSize);

                if (src != null)
                    PhysicalData[Position + oldSize] = src;

                Size = newSize;
            }
        }


        public int Read(Span<T> dest)
        {
            return ReadInternal(dest, dest.Length);
        }

        public T[] Read(int size)
        {
            int readSize = Math.Min(this.Size, size);
            T[] ret = new T[readSize];
            Read(ret);
            return ret;
        }

        public T[] Read() => Read(this.Size);

        int ReadInternal(Span<T> dest, int size)
        {
            checked
            {
                int readSize;

                readSize = Math.Min(size, Size);
                if (readSize == 0)
                {
                    return 0;
                }
                if (dest != null)
                {
                    PhysicalData.AsSpan(this.Position, size).CopyTo(dest);
                }
                Position += readSize;
                Size -= readSize;

                if (Size == 0)
                {
                    Position = 0;
                }

                if (this.Position >= FifoInitSize &&
                    this.PhysicalData.Length >= this.ReAllocMemSize &&
                    (this.PhysicalData.Length / 2) > this.Size)
                {
                    int newPhysicalSize;

                    newPhysicalSize = Math.Max(this.PhysicalData.Length / 2, FifoInitSize);

                    T[] newArray = new T[newPhysicalSize];
                    this.PhysicalData.AsSpan(this.Position, this.Size).CopyTo(newArray);
                    this.PhysicalData = newArray;

                    this.Position = 0;
                }

                return readSize;
            }
        }

        public Span<T> Span { get => this.PhysicalData.AsSpan(this.Position, this.Size); }
    }

    static internal class FastBufferGlobalIdCounter
    {
        static long Id = 0;
        public static long NewId() => Interlocked.Increment(ref Id);
    }

    class FastStreamBuffer<T> : IFastBuffer<Memory<T>>
    {
        FastLinkedList<Memory<T>> List = new FastLinkedList<Memory<T>>();
        public long PinHead { get; private set; } = 0;
        public long PinTail { get; private set; } = 0;
        public long Length { get { long ret = checked(PinTail - PinHead); Debug.Assert(ret >= 0); return ret; } }
        public long Threshold { get; set; }
        public long Id { get; }

        public FastEventListenerList<IFastBufferState, FastBufferCallbackEventType> EventListeners { get; }
            = new FastEventListenerList<IFastBufferState, FastBufferCallbackEventType>();

        public bool IsReadyToWrite
        {
            get
            {
                if (IsDisconnected) return true;
                if (Length <= Threshold) return true;
                CompleteWrite(false);
                return false;
            }
        }

        public bool IsReadyToRead
        {
            get
            {
                if (IsDisconnected) return true;
                if (Length >= 1) return true;
                CompleteRead();
                return false;
            }
        }
        public bool IsEventsEnabled { get; }

        Once internalDisconnectedFlag;
        public bool IsDisconnected { get => internalDisconnectedFlag.IsSet; }

        public AsyncAutoResetEvent EventWriteReady { get; } = null;
        public AsyncAutoResetEvent EventReadReady { get; } = null;

        public const long DefaultThreshold = 524288;

        public List<Action> OnDisconnected { get; } = new List<Action>();

        public object LockObj { get; } = new object();

        public ExceptionQueue ExceptionQueue { get; } = new ExceptionQueue();
        public LayerInfo Info { get; } = new LayerInfo();

        public FastStreamBuffer(bool enableEvents = false, long? thresholdLength = null)
        {
            if (thresholdLength < 0) throw new ArgumentOutOfRangeException("thresholdLength < 0");

            Threshold = thresholdLength ?? DefaultThreshold;
            IsEventsEnabled = enableEvents;
            if (IsEventsEnabled)
            {
                EventWriteReady = new AsyncAutoResetEvent();
                EventReadReady = new AsyncAutoResetEvent();
            }

            Id = FastBufferGlobalIdCounter.NewId();

            EventListeners.Fire(this, FastBufferCallbackEventType.Init);
        }

        Once checkDisconnectFlag;

        public void CheckDisconnected()
        {
            if (IsDisconnected)
            {
                if (checkDisconnectFlag.IsFirstCall())
                {
                    ExceptionQueue.Raise(new FastBufferDisconnectedException());
                }
                else
                {
                    ExceptionQueue.ThrowFirstExceptionIfExists();
                    throw new FastBufferDisconnectedException();
                }
            }
        }

        public void Disconnect()
        {
            if (internalDisconnectedFlag.IsFirstCall())
            {
                foreach (var ev in OnDisconnected)
                {
                    try
                    {
                        ev();
                    }
                    catch { }
                }
                EventReadReady.Set();
                EventWriteReady.Set();

                EventListeners.Fire(this, FastBufferCallbackEventType.Disconnected);
            }
        }

        long LastHeadPin = long.MinValue;

        public void CompleteRead()
        {
            if (IsEventsEnabled)
            {
                bool setFlag = false;

                lock (LockObj)
                {
                    long current = PinHead;
                    if (LastHeadPin != current)
                    {
                        LastHeadPin = current;
                        if (IsReadyToWrite)
                            setFlag = true;
                    }
                    if (IsDisconnected)
                        setFlag = true;
                }

                if (setFlag)
                {
                    EventWriteReady.Set();
                }
            }
        }

        long LastTailPin = long.MinValue;

        public void CompleteWrite(bool checkDisconnect = true)
        {
            if (IsEventsEnabled)
            {
                bool setFlag = false;
                lock (LockObj)
                {
                    long current = PinTail;
                    if (LastTailPin != current)
                    {
                        LastTailPin = current;
                        setFlag = true;
                    }
                    if (IsDisconnected)
                        setFlag = true;
                }
                if (setFlag)
                {
                    EventReadReady.Set();
                }
            }

            if (checkDisconnect)
                CheckDisconnected();
        }

        public void Clear()
        {
            checked
            {
                List.Clear();
                PinTail = PinHead;
            }
        }

        public void InsertBefore(Memory<T> item)
        {
            CheckDisconnected();
            checked
            {
                if (item.IsEmpty) return;
                List.AddFirst(item);
                PinHead -= item.Length;
            }
        }

        public void InsertHead(Memory<T> item)
        {
            CheckDisconnected();
            checked
            {
                if (item.IsEmpty) return;
                List.AddFirst(item);
                PinTail += item.Length;
            }
        }

        public void InsertTail(Memory<T> item)
        {
            CheckDisconnected();
            checked
            {
                if (item.IsEmpty) return;
                List.AddLast(item);
                PinTail += item.Length;
            }
        }

        public void Insert(long pin, Memory<T> item, bool appendIfOverrun = false)
        {
            CheckDisconnected();
            checked
            {
                if (item.IsEmpty) return;

                if (List.First == null)
                {
                    InsertHead(item);
                    return;
                }

                if (appendIfOverrun)
                {
                    if (pin < PinHead)
                        InsertBefore(new T[PinHead - pin]);

                    if (pin > PinTail)
                        InsertTail(new T[pin - PinTail]);
                }
                else
                {
                    if (List.First == null) throw new ArgumentOutOfRangeException("Buffer is empty.");
                    if (pin < PinHead) throw new ArgumentOutOfRangeException("pin < PinHead");
                    if (pin > PinTail) throw new ArgumentOutOfRangeException("pin > PinTail");
                }

                var node = GetNodeWithPin(pin, out int offsetInSegment, out _);
                Debug.Assert(node != null);
                if (offsetInSegment == 0)
                {
                    var newNode = List.AddBefore(node, item);
                    PinTail += item.Length;
                }
                else if (node.Value.Length == offsetInSegment)
                {
                    var newNode = List.AddAfter(node, item);
                    PinTail += item.Length;
                }
                else
                {
                    Memory<T> sliceBefore = node.Value.Slice(0, offsetInSegment);
                    Memory<T> sliceAfter = node.Value.Slice(offsetInSegment);

                    node.Value = sliceBefore;
                    var newNode = List.AddAfter(node, item);
                    List.AddAfter(newNode, sliceAfter);
                    PinTail += item.Length;
                }
            }
        }

        FastLinkedListNode<Memory<T>> GetNodeWithPin(long pin, out int offsetInSegment, out long nodePin)
        {
            checked
            {
                offsetInSegment = 0;
                nodePin = 0;
                if (List.First == null)
                {
                    if (pin != PinHead) throw new ArgumentOutOfRangeException("List.First == null, but pin != PinHead");
                    return null;
                }
                if (pin < PinHead) throw new ArgumentOutOfRangeException("pin < PinHead");
                if (pin == PinHead)
                {
                    nodePin = pin;
                    return List.First;
                }
                if (pin > PinTail) throw new ArgumentOutOfRangeException("pin > PinTail");
                if (pin == PinTail)
                {
                    var last = List.Last;
                    if (last != null)
                    {
                        offsetInSegment = last.Value.Length;
                        nodePin = PinTail - last.Value.Length;
                    }
                    else
                    {
                        nodePin = PinTail;
                    }
                    return last;
                }
                long currentPin = PinHead;
                FastLinkedListNode<Memory<T>> node = List.First;
                while (node != null)
                {
                    if (pin >= currentPin && pin < (currentPin + node.Value.Length))
                    {
                        offsetInSegment = (int)(pin - currentPin);
                        nodePin = currentPin;
                        return node;
                    }
                    currentPin += node.Value.Length;
                    node = node.Next;
                }
                throw new ApplicationException("GetNodeWithPin: Bug!");
            }
        }

        void GetOverlappedNodes(long pinStart, long pinEnd,
            out FastLinkedListNode<Memory<T>> firstNode, out int firstNodeOffsetInSegment, out long firstNodePin,
            out FastLinkedListNode<Memory<T>> lastNode, out int lastNodeOffsetInSegment, out long lastNodePin,
            out int nodeCounts, out int lackRemainLength)
        {
            checked
            {
                if (pinStart > pinEnd) throw new ArgumentOutOfRangeException("pinStart > pinEnd");

                firstNode = GetNodeWithPin(pinStart, out firstNodeOffsetInSegment, out firstNodePin);

                if (pinEnd > PinTail)
                {
                    lackRemainLength = (int)checked(pinEnd - PinTail);
                    pinEnd = PinTail;
                }

                FastLinkedListNode<Memory<T>> node = firstNode;
                long currentPin = pinStart - firstNodeOffsetInSegment;
                nodeCounts = 0;
                while (true)
                {
                    Debug.Assert(node != null, "node == null");

                    nodeCounts++;
                    if (pinEnd <= (currentPin + node.Value.Length))
                    {
                        lastNodeOffsetInSegment = (int)(pinEnd - currentPin);
                        lastNode = node;
                        lackRemainLength = 0;
                        lastNodePin = currentPin;

                        Debug.Assert(firstNodeOffsetInSegment != firstNode.Value.Length);
                        Debug.Assert(lastNodeOffsetInSegment != 0);

                        return;
                    }
                    currentPin += node.Value.Length;
                    node = node.Next;
                }
            }
        }

        public FastBufferSegment<Memory<T>>[] GetSegmentsFast(long pin, long size, out long readSize, bool allowPartial = false)
        {
            checked
            {
                if (size < 0) throw new ArgumentOutOfRangeException("size < 0");
                if (size == 0)
                {
                    readSize = 0;
                    return new FastBufferSegment<Memory<T>>[0];
                }
                if (pin > PinTail)
                {
                    throw new ArgumentOutOfRangeException("pin > PinTail");
                }
                if ((pin + size) > PinTail)
                {
                    if (allowPartial == false)
                        throw new ArgumentOutOfRangeException("(pin + size) > PinTail");
                    size = PinTail - pin;
                }

                FastBufferSegment<Memory<T>>[] ret = GetUncontiguousSegments(pin, pin + size, false);
                readSize = size;
                return ret;
            }
        }

        public FastBufferSegment<Memory<T>>[] ReadForwardFast(ref long pin, long size, out long readSize, bool allowPartial = false)
        {
            checked
            {
                FastBufferSegment<Memory<T>>[] ret = GetSegmentsFast(pin, size, out readSize, allowPartial);
                pin += readSize;
                return ret;
            }
        }

        public Memory<T> GetContiguous(long pin, long size, bool allowPartial = false)
        {
            checked
            {
                if (size < 0) throw new ArgumentOutOfRangeException("size < 0");
                if (size == 0)
                {
                    return new Memory<T>();
                }
                if (pin > PinTail)
                {
                    throw new ArgumentOutOfRangeException("pin > PinTail");
                }
                if ((pin + size) > PinTail)
                {
                    if (allowPartial == false)
                        throw new ArgumentOutOfRangeException("(pin + size) > PinTail");
                    size = PinTail - pin;
                }
                Memory<T> ret = GetContiguousMemory(pin, pin + size, false, false);
                return ret;
            }
        }

        public Memory<T> ReadForwardContiguous(ref long pin, long size, bool allowPartial = false)
        {
            checked
            {
                Memory<T> ret = GetContiguous(pin, size, allowPartial);
                pin += ret.Length;
                return ret;
            }
        }

        public Memory<T> PutContiguous(long pin, long size, bool appendIfOverrun = false)
        {
            checked
            {
                if (size < 0) throw new ArgumentOutOfRangeException("size < 0");
                if (size == 0)
                {
                    return new Memory<T>();
                }
                Memory<T> ret = GetContiguousMemory(pin, pin + size, appendIfOverrun, false);
                return ret;
            }
        }

        public Memory<T> WriteForwardContiguous(ref long pin, long size, bool appendIfOverrun = false)
        {
            checked
            {
                Memory<T> ret = PutContiguous(pin, size, appendIfOverrun);
                pin += ret.Length;
                return ret;
            }
        }

        public void Enqueue(Memory<T> item)
        {
            CheckDisconnected();
            long oldLen = Length;
            if (item.Length == 0) return;
            InsertTail(item);
            EventListeners.Fire(this, FastBufferCallbackEventType.Written);
            if (Length != 0 && oldLen == 0)
                EventListeners.Fire(this, FastBufferCallbackEventType.EmptyToNonEmpty);
        }

        public void EnqueueAllWithLock(Span<Memory<T>> itemList)
        {
            lock (LockObj)
                EnqueueAll(itemList);
        }

        public void EnqueueAll(Span<Memory<T>> itemList)
        {
            CheckDisconnected();
            checked
            {
                int num = 0;
                long oldLen = Length;
                foreach (Memory<T> t in itemList)
                {
                    if (t.Length != 0)
                    {
                        List.AddLast(t);
                        PinTail += t.Length;
                        num++;
                    }
                }
                if (num >= 1)
                {
                    EventListeners.Fire(this, FastBufferCallbackEventType.Written);

                    if (Length != 0 && oldLen == 0)
                        EventListeners.Fire(this, FastBufferCallbackEventType.EmptyToNonEmpty);
                }
            }
        }

        public int DequeueContiguousSlow(Memory<T> dest, int size = int.MaxValue)
        {
            if (IsDisconnected && this.Length == 0) CheckDisconnected();
            checked
            {
                long oldLen = Length;
                if (size < 0) throw new ArgumentOutOfRangeException("size < 0");
                size = Math.Min(size, dest.Length);
                Debug.Assert(size >= 0);
                if (size == 0) return 0;
                var memarray = Dequeue(size, out long totalSize, true);
                Debug.Assert(totalSize <= size);
                if (totalSize > int.MaxValue) throw new IndexOutOfRangeException("totalSize > int.MaxValue");
                if (dest.Length < totalSize) throw new ArgumentOutOfRangeException("dest.Length < totalSize");
                int pos = 0;
                foreach (var mem in memarray)
                {
                    mem.CopyTo(dest.Slice(pos, mem.Length));
                    pos += mem.Length;
                }
                Debug.Assert(pos == totalSize);
                EventListeners.Fire(this, FastBufferCallbackEventType.Read);
                if (Length == 0 && oldLen != 0)
                    EventListeners.Fire(this, FastBufferCallbackEventType.NonEmptyToEmpty);
                return (int)totalSize;
            }
        }

        public Memory<T> DequeueContiguousSlow(int size = int.MaxValue)
        {
            if (IsDisconnected && this.Length == 0) CheckDisconnected();
            checked
            {
                long oldLen = Length;
                if (size < 0) throw new ArgumentOutOfRangeException("size < 0");
                if (size == 0) return Memory<T>.Empty;
                int readSize = (int)Math.Min(size, Length);
                Memory<T> ret = new T[readSize];
                int r = DequeueContiguousSlow(ret, readSize);
                Debug.Assert(r <= readSize);
                ret = ret.Slice(0, r);
                EventListeners.Fire(this, FastBufferCallbackEventType.Read);
                if (Length == 0 && oldLen != 0)
                    EventListeners.Fire(this, FastBufferCallbackEventType.NonEmptyToEmpty);
                return ret;
            }
        }

        public List<Memory<T>> DequeueAllWithLock(out long totalReadSize)
        {
            lock (this.LockObj)
                return DequeueAll(out totalReadSize);
        }
        public List<Memory<T>> DequeueAll(out long totalReadSize) => Dequeue(long.MaxValue, out totalReadSize);
        public List<Memory<T>> Dequeue(long minReadSize, out long totalReadSize, bool allowSplitSegments = true)
        {
            if (IsDisconnected && this.Length == 0) CheckDisconnected();
            checked
            {
                if (minReadSize < 1) throw new ArgumentOutOfRangeException("size < 1");

                totalReadSize = 0;
                if (List.First == null)
                {
                    return new List<Memory<T>>();
                }

                long oldLen = Length;

                FastLinkedListNode<Memory<T>> node = List.First;
                List<Memory<T>> ret = new List<Memory<T>>();
                while (true)
                {
                    if ((totalReadSize + node.Value.Length) >= minReadSize)
                    {
                        if (allowSplitSegments && (totalReadSize + node.Value.Length) > minReadSize)
                        {
                            int lastSegmentReadSize = (int)(minReadSize - totalReadSize);
                            Debug.Assert(lastSegmentReadSize <= node.Value.Length);
                            ret.Add(node.Value.Slice(0, lastSegmentReadSize));
                            if (lastSegmentReadSize == node.Value.Length)
                                List.Remove(node);
                            else
                                node.Value = node.Value.Slice(lastSegmentReadSize);
                            totalReadSize += lastSegmentReadSize;
                            PinHead += totalReadSize;
                            Debug.Assert(minReadSize >= totalReadSize);
                            EventListeners.Fire(this, FastBufferCallbackEventType.Read);
                            if (Length == 0 && oldLen != 0)
                                EventListeners.Fire(this, FastBufferCallbackEventType.NonEmptyToEmpty);
                            return ret;
                        }
                        else
                        {
                            ret.Add(node.Value);
                            totalReadSize += node.Value.Length;
                            List.Remove(node);
                            PinHead += totalReadSize;
                            Debug.Assert(minReadSize <= totalReadSize);
                            EventListeners.Fire(this, FastBufferCallbackEventType.Read);
                            if (Length == 0 && oldLen != 0)
                                EventListeners.Fire(this, FastBufferCallbackEventType.NonEmptyToEmpty);
                            return ret;
                        }
                    }
                    else
                    {
                        ret.Add(node.Value);
                        totalReadSize += node.Value.Length;

                        FastLinkedListNode<Memory<T>> deleteNode = node;
                        node = node.Next;

                        List.Remove(deleteNode);

                        if (node == null)
                        {
                            PinHead += totalReadSize;
                            EventListeners.Fire(this, FastBufferCallbackEventType.Read);
                            if (Length == 0 && oldLen != 0)
                                EventListeners.Fire(this, FastBufferCallbackEventType.NonEmptyToEmpty);
                            return ret;
                        }
                    }
                }
            }
        }

        public long DequeueAllAndEnqueueToOther(IFastBuffer<Memory<T>> other) => DequeueAllAndEnqueueToOther((FastStreamBuffer<T>)other);

        public long DequeueAllAndEnqueueToOther(FastStreamBuffer<T> other)
        {
            if (IsDisconnected && this.Length == 0) CheckDisconnected();
            other.CheckDisconnected();
            checked
            {
                if (this == other) throw new ArgumentException("this == other");

                if (this.Length == 0)
                {
                    Debug.Assert(this.List.Count == 0);
                    return 0;
                }

                if (other.Length == 0)
                {
                    long length = this.Length;
                    long otherOldLen = other.Length;
                    long oldLen = Length;
                    Debug.Assert(other.List.Count == 0);
                    other.List = this.List;
                    this.List = new FastLinkedList<Memory<T>>();
                    this.PinHead = this.PinTail;
                    other.PinTail += length;
                    EventListeners.Fire(this, FastBufferCallbackEventType.Read);
                    if (Length == 0 && oldLen != 0)
                        EventListeners.Fire(this, FastBufferCallbackEventType.NonEmptyToEmpty);
                    other.EventListeners.Fire(other, FastBufferCallbackEventType.Written);
                    if (other.Length != 0 && otherOldLen == 0)
                        other.EventListeners.Fire(other, FastBufferCallbackEventType.EmptyToNonEmpty);
                    return length;
                }
                else
                {
                    long length = this.Length;
                    long oldLen = Length;
                    long otherOldLen = other.Length;
                    var chainFirst = this.List.First;
                    var chainLast = this.List.Last;
                    other.List.AddLast(this.List.First, this.List.Last, this.List.Count);
                    this.List.Clear();
                    this.PinHead = this.PinTail;
                    other.PinTail += length;
                    EventListeners.Fire(this, FastBufferCallbackEventType.Read);
                    if (Length == 0 && oldLen != 0)
                        EventListeners.Fire(this, FastBufferCallbackEventType.NonEmptyToEmpty);
                    other.EventListeners.Fire(other, FastBufferCallbackEventType.Written);
                    if (other.Length != 0 && otherOldLen == 0)
                        other.EventListeners.Fire(other, FastBufferCallbackEventType.EmptyToNonEmpty);
                    return length;
                }
            }
        }

        FastBufferSegment<Memory<T>>[] GetUncontiguousSegments(long pinStart, long pinEnd, bool appendIfOverrun)
        {
            checked
            {
                if (pinStart == pinEnd) return new FastBufferSegment<Memory<T>>[0];
                if (pinStart > pinEnd) throw new ArgumentOutOfRangeException("pinStart > pinEnd");

                if (appendIfOverrun)
                {
                    if (List.First == null)
                    {
                        InsertHead(new T[pinEnd - pinStart]);
                        PinHead = pinStart;
                        PinTail = pinEnd;
                    }

                    if (pinStart < PinHead)
                        InsertBefore(new T[PinHead - pinStart]);

                    if (pinEnd > PinTail)
                        InsertTail(new T[pinEnd - PinTail]);
                }
                else
                {
                    if (List.First == null) throw new ArgumentOutOfRangeException("Buffer is empty.");
                    if (pinStart < PinHead) throw new ArgumentOutOfRangeException("pinStart < PinHead");
                    if (pinEnd > PinTail) throw new ArgumentOutOfRangeException("pinEnd > PinTail");
                }

                GetOverlappedNodes(pinStart, pinEnd,
                    out FastLinkedListNode<Memory<T>> firstNode, out int firstNodeOffsetInSegment, out long firstNodePin,
                    out FastLinkedListNode<Memory<T>> lastNode, out int lastNodeOffsetInSegment, out long lastNodePin,
                    out int nodeCounts, out int lackRemainLength);

                Debug.Assert(lackRemainLength == 0, "lackRemainLength != 0");

                if (firstNode == lastNode)
                    return new FastBufferSegment<Memory<T>>[1]{ new FastBufferSegment<Memory<T>>(
                    firstNode.Value.Slice(firstNodeOffsetInSegment, lastNodeOffsetInSegment - firstNodeOffsetInSegment), pinStart, 0) };

                FastBufferSegment<Memory<T>>[] ret = new FastBufferSegment<Memory<T>>[nodeCounts];

                FastLinkedListNode<Memory<T>> prevNode = firstNode.Previous;
                FastLinkedListNode<Memory<T>> nextNode = lastNode.Next;

                FastLinkedListNode<Memory<T>> node = firstNode;
                int count = 0;
                long currentOffset = 0;

                while (true)
                {
                    Debug.Assert(node != null, "node == null");

                    int sliceStart = (node == firstNode) ? firstNodeOffsetInSegment : 0;
                    int sliceLength = (node == lastNode) ? lastNodeOffsetInSegment : node.Value.Length - sliceStart;

                    ret[count] = new FastBufferSegment<Memory<T>>(node.Value.Slice(sliceStart, sliceLength), currentOffset + pinStart, currentOffset);
                    count++;

                    Debug.Assert(count <= nodeCounts, "count > nodeCounts");

                    currentOffset += sliceLength;

                    if (node == lastNode)
                    {
                        Debug.Assert(count == ret.Length, "count != ret.Length");
                        break;
                    }

                    node = node.Next;
                }

                return ret;
            }
        }

        public void Remove(long pinStart, long length)
        {
            checked
            {
                if (length == 0) return;
                if (length < 0) throw new ArgumentOutOfRangeException("length < 0");
                long pinEnd = checked(pinStart + length);
                if (List.First == null) throw new ArgumentOutOfRangeException("Buffer is empty.");
                if (pinStart < PinHead) throw new ArgumentOutOfRangeException("pinStart < PinHead");
                if (pinEnd > PinTail) throw new ArgumentOutOfRangeException("pinEnd > PinTail");

                GetOverlappedNodes(pinStart, pinEnd,
                    out FastLinkedListNode<Memory<T>> firstNode, out int firstNodeOffsetInSegment, out long firstNodePin,
                    out FastLinkedListNode<Memory<T>> lastNode, out int lastNodeOffsetInSegment, out long lastNodePin,
                    out int nodeCounts, out int lackRemainLength);

                Debug.Assert(lackRemainLength == 0, "lackRemainLength != 0");

                if (firstNode == lastNode)
                {
                    Debug.Assert(firstNodeOffsetInSegment < lastNodeOffsetInSegment);
                    if (firstNodeOffsetInSegment == 0 && lastNodeOffsetInSegment == lastNode.Value.Length)
                    {
                        Debug.Assert(firstNode.Value.Length == length, "firstNode.Value.Length != length");
                        List.Remove(firstNode);
                        PinTail -= length;
                        return;
                    }
                    else
                    {
                        Debug.Assert((lastNodeOffsetInSegment - firstNodeOffsetInSegment) == length);
                        Memory<T> slice1 = firstNode.Value.Slice(0, firstNodeOffsetInSegment);
                        Memory<T> slice2 = firstNode.Value.Slice(lastNodeOffsetInSegment);
                        Debug.Assert(slice1.Length != 0 || slice2.Length != 0);
                        if (slice1.Length == 0)
                        {
                            firstNode.Value = slice2;
                        }
                        else if (slice2.Length == 0)
                        {
                            firstNode.Value = slice1;
                        }
                        else
                        {
                            firstNode.Value = slice1;
                            List.AddAfter(firstNode, slice2);
                        }
                        PinTail -= length;
                        return;
                    }
                }
                else
                {
                    firstNode.Value = firstNode.Value.Slice(0, firstNodeOffsetInSegment);
                    lastNode.Value = lastNode.Value.Slice(lastNodeOffsetInSegment);

                    var node = firstNode.Next;
                    while (node != lastNode)
                    {
                        var nodeToDelete = node;

                        Debug.Assert(node.Next != null);
                        node = node.Next;

                        List.Remove(nodeToDelete);
                    }

                    if (lastNode.Value.Length == 0)
                        List.Remove(lastNode);

                    if (firstNode.Value.Length == 0)
                        List.Remove(firstNode);

                    PinTail -= length;
                    return;
                }
            }
        }

        public T[] ToArray() => GetContiguousMemory(PinHead, PinTail, false, true).ToArray();

        public T[] ItemsSlow { get => ToArray(); }

        Memory<T> GetContiguousMemory(long pinStart, long pinEnd, bool appendIfOverrun, bool noReplace)
        {
            checked
            {
                if (pinStart == pinEnd) return new Memory<T>();
                if (pinStart > pinEnd) throw new ArgumentOutOfRangeException("pinStart > pinEnd");

                if (appendIfOverrun)
                {
                    if (List.First == null)
                    {
                        InsertHead(new T[pinEnd - pinStart]);
                        PinHead = pinStart;
                        PinTail = pinEnd;
                    }

                    if (pinStart < PinHead)
                        InsertBefore(new T[PinHead - pinStart]);

                    if (pinEnd > PinTail)
                        InsertTail(new T[pinEnd - PinTail]);
                }
                else
                {
                    if (List.First == null) throw new ArgumentOutOfRangeException("Buffer is empty.");
                    if (pinStart < PinHead) throw new ArgumentOutOfRangeException("pinStart < PinHead");
                    if (pinEnd > PinTail) throw new ArgumentOutOfRangeException("pinEnd > PinTail");
                }

                GetOverlappedNodes(pinStart, pinEnd,
                    out FastLinkedListNode<Memory<T>> firstNode, out int firstNodeOffsetInSegment, out long firstNodePin,
                    out FastLinkedListNode<Memory<T>> lastNode, out int lastNodeOffsetInSegment, out long lastNodePin,
                    out int nodeCounts, out int lackRemainLength);

                Debug.Assert(lackRemainLength == 0, "lackRemainLength != 0");

                if (firstNode == lastNode)
                    return firstNode.Value.Slice(firstNodeOffsetInSegment, lastNodeOffsetInSegment - firstNodeOffsetInSegment);

                FastLinkedListNode<Memory<T>> prevNode = firstNode.Previous;
                FastLinkedListNode<Memory<T>> nextNode = lastNode.Next;

                Memory<T> newMemory = new T[lastNodePin + lastNode.Value.Length - firstNodePin];
                FastLinkedListNode<Memory<T>> node = firstNode;
                int currentWritePointer = 0;

                while (true)
                {
                    Debug.Assert(node != null, "node == null");

                    bool finish = false;
                    node.Value.CopyTo(newMemory.Slice(currentWritePointer));

                    if (node == lastNode) finish = true;

                    FastLinkedListNode<Memory<T>> nodeToDelete = node;
                    currentWritePointer += node.Value.Length;

                    node = node.Next;

                    if (noReplace == false)
                        List.Remove(nodeToDelete);

                    if (finish) break;
                }

                if (noReplace == false)
                {
                    if (prevNode != null)
                        List.AddAfter(prevNode, newMemory);
                    else if (nextNode != null)
                        List.AddBefore(nextNode, newMemory);
                    else
                        List.AddFirst(newMemory);
                }

                var ret = newMemory.Slice(firstNodeOffsetInSegment, newMemory.Length - (lastNode.Value.Length - lastNodeOffsetInSegment) - firstNodeOffsetInSegment);
                Debug.Assert(ret.Length == (pinEnd - pinStart), "ret.Length");
                return ret;
            }
        }

        public static implicit operator FastStreamBuffer<T>(Memory<T> memory)
        {
            FastStreamBuffer<T> ret = new FastStreamBuffer<T>(false, null);
            ret.Enqueue(memory);
            return ret;
        }

        public static implicit operator FastStreamBuffer<T>(Span<T> span) => span.ToArray().AsMemory();
        public static implicit operator FastStreamBuffer<T>(T[] data) => data.AsMemory();
    }





    class FastDatagramBuffer<T> : IFastBuffer<T>
    {
        Fifo<T> Fifo = new Fifo<T>();

        public long PinHead { get; private set; } = 0;
        public long PinTail { get; private set; } = 0;
        public long Length { get { long ret = checked(PinTail - PinHead); Debug.Assert(ret >= 0); return ret; } }
        public long Threshold { get; set; }
        public long Id { get; }

        public FastEventListenerList<IFastBufferState, FastBufferCallbackEventType> EventListeners { get; }
            = new FastEventListenerList<IFastBufferState, FastBufferCallbackEventType>();

        public bool IsReadyToWrite
        {
            get
            {
                if (IsDisconnected) return true;
                if (Length <= Threshold) return true;
                CompleteWrite(false);
                return false;
            }
        }

        public bool IsReadyToRead
        {
            get
            {
                if (IsDisconnected) return true;
                if (Length >= 1) return true;
                CompleteRead();
                return false;
            }
        }

        public bool IsEventsEnabled { get; }

        public AsyncAutoResetEvent EventWriteReady { get; } = null;
        public AsyncAutoResetEvent EventReadReady { get; } = null;

        Once internalDisconnectedFlag;
        public bool IsDisconnected { get => internalDisconnectedFlag.IsSet; }

        public List<Action> OnDisconnected { get; } = new List<Action>();

        public const long DefaultThreshold = 65536;

        public object LockObj { get; } = new object();

        public ExceptionQueue ExceptionQueue { get; } = new ExceptionQueue();
        public LayerInfo Info { get; } = new LayerInfo();

        public FastDatagramBuffer(bool enableEvents = false, long? thresholdLength = null)
        {
            if (thresholdLength < 0) throw new ArgumentOutOfRangeException("thresholdLength < 0");

            Threshold = thresholdLength ?? DefaultThreshold;
            IsEventsEnabled = enableEvents;
            if (IsEventsEnabled)
            {
                EventWriteReady = new AsyncAutoResetEvent();
                EventReadReady = new AsyncAutoResetEvent();
            }

            Id = FastBufferGlobalIdCounter.NewId();

            EventListeners.Fire(this, FastBufferCallbackEventType.Init);
        }

        Once checkDisconnectFlag;

        public void CheckDisconnected()
        {
            if (IsDisconnected)
            {
                if (checkDisconnectFlag.IsFirstCall())
                    ExceptionQueue.Raise(new FastBufferDisconnectedException());
                else
                {
                    ExceptionQueue.ThrowFirstExceptionIfExists();
                    throw new FastBufferDisconnectedException();
                }
            }
        }

        public void Disconnect()
        {
            if (internalDisconnectedFlag.IsFirstCall())
            {
                foreach (var ev in OnDisconnected)
                {
                    try
                    {
                        ev();
                    }
                    catch { }
                }
                EventReadReady.Set();
                EventWriteReady.Set();

                EventListeners.Fire(this, FastBufferCallbackEventType.Disconnected);
            }
        }

        long LastHeadPin = long.MinValue;

        public void CompleteRead()
        {
            if (IsEventsEnabled)
            {
                bool setFlag = false;

                lock (LockObj)
                {
                    long current = PinHead;
                    if (LastHeadPin != current)
                    {
                        LastHeadPin = current;
                        if (IsReadyToWrite)
                            setFlag = true;
                    }
                    if (IsDisconnected)
                        setFlag = true;
                }

                if (setFlag)
                {
                    EventWriteReady.Set();
                }
            }
        }

        long LastTailPin = long.MinValue;

        public void CompleteWrite(bool checkDisconnect = true)
        {
            if (IsEventsEnabled)
            {
                bool setFlag = false;

                lock (LockObj)
                {
                    long current = PinTail;
                    if (LastTailPin != current)
                    {
                        LastTailPin = current;
                        setFlag = true;
                    }
                }

                if (setFlag)
                {
                    EventReadReady.Set();
                }
            }
            if (checkDisconnect)
                CheckDisconnected();
        }

        public void Clear()
        {
            checked
            {
                Fifo.Clear();
                PinTail = PinHead;
            }
        }

        public void Enqueue(T item)
        {
            CheckDisconnected();
            checked
            {
                long oldLen = Length;
                Fifo.Write(item);
                PinTail++;
                EventListeners.Fire(this, FastBufferCallbackEventType.Written);
                if (Length != 0 && oldLen == 0)
                    EventListeners.Fire(this, FastBufferCallbackEventType.EmptyToNonEmpty);
            }
        }

        public void EnqueueAllWithLock(Span<T> itemList)
        {
            lock (LockObj)
                EnqueueAll(itemList);
        }

        public void EnqueueAll(Span<T> itemList)
        {
            CheckDisconnected();
            checked
            {
                long oldLen = Length;
                Fifo.Write(itemList);
                PinTail += itemList.Length;
                EventListeners.Fire(this, FastBufferCallbackEventType.Written);
                if (Length != 0 && oldLen == 0)
                    EventListeners.Fire(this, FastBufferCallbackEventType.EmptyToNonEmpty);
            }
        }

        public List<T> Dequeue(long minReadSize, out long totalReadSize, bool allowSplitSegments = true)
        {
            if (IsDisconnected && this.Length == 0) CheckDisconnected();
            checked
            {
                if (minReadSize < 1) throw new ArgumentOutOfRangeException("size < 1");
                if (minReadSize >= int.MaxValue) minReadSize = int.MaxValue;

                long oldLen = Length;

                totalReadSize = 0;
                if (Fifo.Size == 0)
                {
                    return new List<T>();
                }

                T[] tmp = Fifo.Read((int)minReadSize);

                totalReadSize = tmp.Length;
                List<T> ret = new List<T>(tmp);

                PinHead += totalReadSize;

                EventListeners.Fire(this, FastBufferCallbackEventType.Read);

                if (Length == 0 && oldLen != 0)
                    EventListeners.Fire(this, FastBufferCallbackEventType.NonEmptyToEmpty);

                return ret;
            }
        }

        public List<T> DequeueAll(out long totalReadSize) => Dequeue(long.MaxValue, out totalReadSize);

        public List<T> DequeueAllWithLock(out long totalReadSize)
        {
            lock (LockObj)
                return DequeueAll(out totalReadSize);
        }

        public long DequeueAllAndEnqueueToOther(IFastBuffer<T> other) => DequeueAllAndEnqueueToOther((FastDatagramBuffer<T>)other);

        public long DequeueAllAndEnqueueToOther(FastDatagramBuffer<T> other)
        {
            if (IsDisconnected && this.Length == 0) CheckDisconnected();
            other.CheckDisconnected();
            checked
            {
                if (this == other) throw new ArgumentException("this == other");

                if (this.Length == 0)
                {
                    Debug.Assert(this.Fifo.Size == 0);
                    return 0;
                }

                if (other.Length == 0)
                {
                    long oldLen = Length;
                    long length = this.Length;
                    Debug.Assert(other.Fifo.Size == 0);
                    other.Fifo = this.Fifo;
                    this.Fifo = new Fifo<T>();
                    this.PinHead = this.PinTail;
                    other.PinTail += length;
                    EventListeners.Fire(this, FastBufferCallbackEventType.Read);
                    other.EventListeners.Fire(other, FastBufferCallbackEventType.Written);
                    if (Length != 0 && oldLen == 0)
                        EventListeners.Fire(this, FastBufferCallbackEventType.EmptyToNonEmpty);
                    return length;
                }
                else
                {
                    long oldLen = Length;
                    long length = this.Length;
                    var data = this.Fifo.Read();
                    other.Fifo.Write(data);
                    this.PinHead = this.PinTail;
                    other.PinTail += length;
                    EventListeners.Fire(this, FastBufferCallbackEventType.Read);
                    other.EventListeners.Fire(other, FastBufferCallbackEventType.Written);
                    if (Length != 0 && oldLen == 0)
                        EventListeners.Fire(this, FastBufferCallbackEventType.EmptyToNonEmpty);
                    return length;
                }
            }
        }


        public T[] ToArray() => Fifo.Span.ToArray();

        public T[] ItemsSlow { get => ToArray(); }
    }

    class FastStreamFifo : FastStreamBuffer<byte>
    {
        public FastStreamFifo(bool enableEvents = false, long? thresholdLength = null)
            : base(enableEvents, thresholdLength) { }
    }

    class FastDatagramFifo : FastDatagramBuffer<Datagram>
    {
        public FastDatagramFifo(bool enableEvents = false, long? thresholdLength = null)
            : base(enableEvents, thresholdLength) { }
    }

    static class FastPipeHelper
    {
        public static async Task WaitForReadyToWrite(this IFastBufferState writer, CancellationToken cancel, int timeout)
        {
            LocalTimer timer = new LocalTimer();

            timer.AddTimeout(FastPipeGlobalConfig.PollingTimeout);
            long timeoutTick = timer.AddTimeout(timeout);

            while (writer.IsReadyToWrite == false)
            {
                if (FastTick64.Now >= timeoutTick) throw new TimeoutException();
                cancel.ThrowIfCancellationRequested();

                await WebSocketHelper.WaitObjectsAsync(
                    cancels: new CancellationToken[] { cancel },
                    events: new AsyncAutoResetEvent[] { writer.EventWriteReady },
                    timeout: timer.GetNextInterval()
                    );
            }

            cancel.ThrowIfCancellationRequested();
        }

        public static async Task WaitForReadyToRead(this IFastBufferState reader, CancellationToken cancel, int timeout)
        {
            LocalTimer timer = new LocalTimer();

            timer.AddTimeout(FastPipeGlobalConfig.PollingTimeout);
            long timeoutTick = timer.AddTimeout(timeout);

            while (reader.IsReadyToRead == false)
            {
                if (FastTick64.Now >= timeoutTick) throw new TimeoutException();
                cancel.ThrowIfCancellationRequested();

                await WebSocketHelper.WaitObjectsAsync(
                    cancels: new CancellationToken[] { cancel },
                    events: new AsyncAutoResetEvent[] { reader.EventReadReady },
                    timeout: timer.GetNextInterval()
                    );
            }

            cancel.ThrowIfCancellationRequested();
        }
    }

    static class FastPipeGlobalConfig
    {
        public static int MaxStreamBufferLength = 4 * 65536;
        public static int MaxDatagramQueueLength = 65536;
        public static int MaxPollingTimeout = 256 * 1000;

        public static void ApplyHeavyLoadServerConfig()
        {
            MaxStreamBufferLength = 65536;
            MaxPollingTimeout = 4321;
            MaxDatagramQueueLength = 1024;
        }

        public static int PollingTimeout
        {
            get
            {
                int v = MaxPollingTimeout / 10;
                if (v != 0)
                    v = WebSocketHelper.RandSInt31() % v;
                return MaxPollingTimeout - v;
            }
        }
    }

    class FastPipe : AsyncCleanupable
    {
        public CancelWatcher CancelWatcher { get; }

        FastStreamFifo StreamAtoB;
        FastStreamFifo StreamBtoA;
        FastDatagramFifo DatagramAtoB;
        FastDatagramFifo DatagramBtoA;

        public ExceptionQueue ExceptionQueue { get; } = new ExceptionQueue();
        public LayerInfo Info { get; } = new LayerInfo();

        public FastPipeEnd A_LowerSide { get; }
        public FastPipeEnd B_UpperSide { get; }

        public FastPipeEnd this[FastPipeEndSide side]
        {
            get
            {
                if (side == FastPipeEndSide.A_LowerSide)
                    return A_LowerSide;
                else if (side == FastPipeEndSide.B_UpperSide)
                    return B_UpperSide;
                else
                    throw new ArgumentOutOfRangeException("side");
            }
        }

        public List<Action> OnDisconnected { get; } = new List<Action>();

        public AsyncManualResetEvent OnDisconnectedEvent { get; } = new AsyncManualResetEvent();

        Once internalDisconnectedFlag;
        public bool IsDisconnected { get => internalDisconnectedFlag.IsSet; }

        public FastPipe(AsyncCleanuperLady lady, CancellationToken cancel = default, long? thresholdLengthStream = null, long? thresholdLengthDatagram = null)
            : base(lady)
        {
            try
            {
                CancelWatcher = new CancelWatcher(lady, cancel);

                if (thresholdLengthStream == null) thresholdLengthStream = FastPipeGlobalConfig.MaxStreamBufferLength;
                if (thresholdLengthDatagram == null) thresholdLengthDatagram = FastPipeGlobalConfig.MaxDatagramQueueLength;

                StreamAtoB = new FastStreamFifo(true, thresholdLengthStream);
                StreamBtoA = new FastStreamFifo(true, thresholdLengthStream);

                DatagramAtoB = new FastDatagramFifo(true, thresholdLengthDatagram);
                DatagramBtoA = new FastDatagramFifo(true, thresholdLengthDatagram);

                StreamAtoB.ExceptionQueue.Encounter(ExceptionQueue);
                StreamBtoA.ExceptionQueue.Encounter(ExceptionQueue);

                DatagramAtoB.ExceptionQueue.Encounter(ExceptionQueue);
                DatagramBtoA.ExceptionQueue.Encounter(ExceptionQueue);

                StreamAtoB.Info.Encounter(Info);
                StreamBtoA.Info.Encounter(Info);

                DatagramAtoB.Info.Encounter(Info);
                DatagramBtoA.Info.Encounter(Info);

                StreamAtoB.OnDisconnected.Add(() => Disconnect());
                StreamBtoA.OnDisconnected.Add(() => Disconnect());

                DatagramAtoB.OnDisconnected.Add(() => Disconnect());
                DatagramBtoA.OnDisconnected.Add(() => Disconnect());

                A_LowerSide = new FastPipeEnd(this, FastPipeEndSide.A_LowerSide, CancelWatcher, StreamAtoB, StreamBtoA, DatagramAtoB, DatagramBtoA);
                B_UpperSide = new FastPipeEnd(this, FastPipeEndSide.B_UpperSide, CancelWatcher, StreamBtoA, StreamAtoB, DatagramBtoA, DatagramAtoB);

                A_LowerSide._InternalSetCounterPart(B_UpperSide);
                B_UpperSide._InternalSetCounterPart(A_LowerSide);

                CancelWatcher.CancelToken.Register(() =>
                {
                    Disconnect(new OperationCanceledException());
                });
            }
            catch
            {
                Lady.DisposeAllSafe();
                throw;
            }
        }

        object LayerInfoLock = new object();

        public LayerInfoBase LayerInfo_A_LowerSide { get; private set; } = null;
        public LayerInfoBase LayerInfo_B_UpperSide { get; private set; } = null;

        public class InstalledLayerHolder : Holder<LayerInfoBase>
        {
            internal InstalledLayerHolder(Action<LayerInfoBase> disposeProc, LayerInfoBase userData = null) : base(disposeProc, userData) { }
        }

        internal InstalledLayerHolder _InternalInstallLayerInfo(FastPipeEndSide side, LayerInfoBase info)
        {
            if (info == null)
                throw new ArgumentNullException("info");

            lock (LayerInfoLock)
            {
                if (side == FastPipeEndSide.A_LowerSide)
                {
                    if (LayerInfo_A_LowerSide != null) throw new ApplicationException("LayerInfo_A_LowerSide is already installed.");
                    Info.Install(info, LayerInfo_B_UpperSide, false);
                    LayerInfo_A_LowerSide = info;
                }
                else
                {
                    if (LayerInfo_B_UpperSide != null) throw new ApplicationException("LayerInfo_B_UpperSide is already installed.");
                    Info.Install(info, LayerInfo_A_LowerSide, true);
                    LayerInfo_B_UpperSide = info;
                }

                return new InstalledLayerHolder(x =>
                {
                    lock (LayerInfoLock)
                    {
                        if (side == FastPipeEndSide.A_LowerSide)
                        {
                            Debug.Assert(LayerInfo_A_LowerSide != null);
                            Info.Uninstall(LayerInfo_A_LowerSide);
                            LayerInfo_A_LowerSide = null;
                        }
                        else
                        {
                            Debug.Assert(LayerInfo_B_UpperSide != null);
                            Info.Uninstall(LayerInfo_B_UpperSide);
                            LayerInfo_B_UpperSide = null;
                        }
                    }
                },
                info);
            }
        }

        public void Disconnect(Exception ex = null)
        {
            if (internalDisconnectedFlag.IsFirstCall())
            {
                if (ex != null)
                {
                    ExceptionQueue.Add(ex);
                }

                Action[] evList;
                lock (OnDisconnected)
                    evList = OnDisconnected.ToArray();

                foreach (var ev in evList)
                {
                    try
                    {
                        ev();
                    }
                    catch { }
                }

                StreamAtoB.Disconnect();
                StreamBtoA.Disconnect();

                DatagramAtoB.Disconnect();
                DatagramBtoA.Disconnect();

                OnDisconnectedEvent.Set(true);
            }
        }

        Once DisposeFlag;
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!disposing || DisposeFlag.IsFirstCall() == false) return;
                Disconnect();
            }
            finally { base.Dispose(disposing); }
        }

        public void CheckDisconnected()
        {
            StreamAtoB.CheckDisconnected();
            StreamBtoA.CheckDisconnected();
            DatagramAtoB.CheckDisconnected();
            DatagramBtoA.CheckDisconnected();
        }
    }

    [Flags]
    enum FastPipeEndSide
    {
        A_LowerSide,
        B_UpperSide,
    }

    [Flags]
    enum FastPipeEndAttachDirection
    {
        NoAttach,
        A_LowerSide,
        B_UpperSide,
    }

    class FastPipeEnd
    {
        public FastPipe Pipe { get; }

        public FastPipeEndSide Side { get; }

        public CancelWatcher CancelWatcher { get; }
        public FastStreamFifo StreamWriter { get; }
        public FastStreamFifo StreamReader { get; }
        public FastDatagramFifo DatagramWriter { get; }
        public FastDatagramFifo DatagramReader { get; }

        public FastPipeEnd CounterPart { get; private set; }

        public AsyncManualResetEvent OnDisconnectedEvent { get => Pipe.OnDisconnectedEvent; }

        public ExceptionQueue ExceptionQueue { get => Pipe.ExceptionQueue; }
        public LayerInfo LayerInfo { get => Pipe.Info; }

        public bool IsDisconnected { get => this.Pipe.IsDisconnected; }
        public void Disconnect(Exception ex = null) { this.Pipe.Disconnect(ex); }
        public void AddOnDisconnected(Action action)
        {
            lock (Pipe.OnDisconnected)
                Pipe.OnDisconnected.Add(action);
        }

        public static FastPipeEnd NewFastPipeAndGetOneSide(FastPipeEndSide createNewPipeAndReturnThisSide, AsyncCleanuperLady lady, CancellationToken cancel = default, long? thresholdLengthStream = null, long? thresholdLengthDatagram = null)
        {
            var pipe = new FastPipe(lady, cancel, thresholdLengthStream, thresholdLengthDatagram);
            return pipe[createNewPipeAndReturnThisSide];
        }

        internal FastPipeEnd(FastPipe pipe, FastPipeEndSide side,
            CancelWatcher cancelWatcher,
            FastStreamFifo streamToWrite, FastStreamFifo streamToRead,
            FastDatagramFifo datagramToWrite, FastDatagramFifo datagramToRead)
        {
            this.Side = side;
            this.Pipe = pipe;
            this.CancelWatcher = cancelWatcher;
            this.StreamWriter = streamToWrite;
            this.StreamReader = streamToRead;
            this.DatagramWriter = datagramToWrite;
            this.DatagramReader = datagramToRead;
        }

        internal void _InternalSetCounterPart(FastPipeEnd p)
            => this.CounterPart = p;

        internal object _InternalAttachHandleLock = new object();
        internal FastAttachHandle _InternalCurrentAttachHandle = null;

        public FastAttachHandle Attach(AsyncCleanuperLady lady, FastPipeEndAttachDirection attachDirection, object userState = null) => new FastAttachHandle(lady, this, attachDirection, userState);

        internal FastPipeEndStream _InternalGetStream(AsyncCleanuperLady lady, bool autoFlush = true)
            => new FastPipeEndStream(this, autoFlush);

        public FastAppStub GetFastAppProtocolStub(AsyncCleanuperLady lady, CancellationToken cancel = default)
            => new FastAppStub(lady, this, cancel);

        public void CheckDisconnected() => Pipe.CheckDisconnected();
    }

    class FastAttachHandle : AsyncCleanupable
    {
        public FastPipeEnd PipeEnd { get; }
        public object UserState { get; }
        public FastPipeEndAttachDirection Direction { get; }

        FastPipe.InstalledLayerHolder InstalledLayerHolder = null;

        LeakCheckerHolder Leak;
        CriticalSection LockObj = new CriticalSection();

        public FastAttachHandle(AsyncCleanuperLady lady, FastPipeEnd end, FastPipeEndAttachDirection attachDirection, object userState = null)
            : base(lady)
        {
            try
            {
                if (end.Side == FastPipeEndSide.A_LowerSide)
                    Direction = FastPipeEndAttachDirection.A_LowerSide;
                else
                    Direction = FastPipeEndAttachDirection.B_UpperSide;

                if (attachDirection != Direction)
                    throw new ArgumentException($"attachDirection ({attachDirection}) != {Direction}");

                end.CheckDisconnected();

                lock (end._InternalAttachHandleLock)
                {
                    if (end._InternalCurrentAttachHandle != null)
                        throw new ApplicationException("The FastPipeEnd is already attached.");

                    this.UserState = userState;
                    this.PipeEnd = end;
                    this.PipeEnd._InternalCurrentAttachHandle = this;
                }

                Leak = LeakChecker.Enter().AddToLady(this);
            }
            catch
            {
                Lady.DisposeAllSafe();
                throw;
            }
        }

        public void SetLayerInfo(LayerInfoBase info, FastStackBase protocolStack = null)
        {
            lock (LockObj)
            {
                if (DisposeFlag.IsSet) return;

                if (InstalledLayerHolder != null)
                    throw new ApplicationException("LayerInfo is already set.");

                info._InternalSetProtocolStack(protocolStack);

                InstalledLayerHolder = PipeEnd.Pipe._InternalInstallLayerInfo(PipeEnd.Side, info).AddToLady(this);
            }
        }

        int receiveTimeoutProcId = 0;
        TimeoutDetector receiveTimeoutDetector = null;

        public void SetStreamTimeout(int recvTimeout = Timeout.Infinite, int sendTimeout = Timeout.Infinite)
        {
            SetStreamReceiveTimeout(recvTimeout);
            SetStreamSendTimeout(sendTimeout);
        }

        public void SetStreamReceiveTimeout(int timeout = Timeout.Infinite)
        {
            if (Direction == FastPipeEndAttachDirection.A_LowerSide)
                throw new ApplicationException("The attachment direction is From_Lower_To_A_LowerSide.");

            lock (LockObj)
            {
                if (timeout < 0 || timeout == int.MaxValue)
                {
                    if (receiveTimeoutProcId != 0)
                    {
                        PipeEnd.StreamReader.EventListeners.UnregisterCallback(receiveTimeoutProcId);
                        receiveTimeoutProcId = 0;
                        receiveTimeoutDetector.DisposeSafe();
                    }
                }
                else
                {
                    if (DisposeFlag.IsSet) return;

                    SetStreamReceiveTimeout(Timeout.Infinite);

                    receiveTimeoutDetector = new TimeoutDetector(new AsyncCleanuperLady(), timeout, callback: (x) =>
                    {
                        if (PipeEnd.StreamReader.IsReadyToWrite == false)
                            return true;
                        PipeEnd.Pipe.Disconnect(new TimeoutException("StreamReceiveTimeout"));
                        return false;
                    });

                    receiveTimeoutProcId = PipeEnd.StreamReader.EventListeners.RegisterCallback((buffer, type, state) =>
                    {
                        if (type == FastBufferCallbackEventType.Written || type == FastBufferCallbackEventType.NonEmptyToEmpty)
                            receiveTimeoutDetector.Keep();
                    });
                }
            }
        }

        int sendTimeoutProcId = 0;
        TimeoutDetector sendTimeoutDetector = null;

        public void SetStreamSendTimeout(int timeout = Timeout.Infinite)
        {
            if (Direction == FastPipeEndAttachDirection.A_LowerSide)
                throw new ApplicationException("The attachment direction is From_Lower_To_A_LowerSide.");

            lock (LockObj)
            {
                if (timeout < 0 || timeout == int.MaxValue)
                {
                    if (sendTimeoutProcId != 0)
                    {
                        PipeEnd.StreamWriter.EventListeners.UnregisterCallback(sendTimeoutProcId);
                        sendTimeoutProcId = 0;
                        sendTimeoutDetector.DisposeSafe();
                    }
                }
                else
                {
                    if (DisposeFlag.IsSet) return;

                    SetStreamSendTimeout(Timeout.Infinite);

                    sendTimeoutDetector = new TimeoutDetector(new AsyncCleanuperLady(), timeout, callback: (x) =>
                    {
                        if (PipeEnd.StreamWriter.IsReadyToRead == false)
                            return true;

                        PipeEnd.Pipe.Disconnect(new TimeoutException("StreamSendTimeout"));
                        return false;
                    });

                    sendTimeoutProcId = PipeEnd.StreamWriter.EventListeners.RegisterCallback((buffer, type, state) =>
                    {
                    //                            WriteLine($"{type}  {buffer.Length}  {buffer.IsReadyToWrite}");
                    if (type == FastBufferCallbackEventType.Read || type == FastBufferCallbackEventType.EmptyToNonEmpty || type == FastBufferCallbackEventType.PartialProcessReadData)
                            sendTimeoutDetector.Keep();
                    });
                }
            }
        }

        public FastPipeEndStream GetStream(bool autoFlush = true)
            => PipeEnd._InternalGetStream(this.Lady, autoFlush);

        Once DisposeFlag;
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!disposing || DisposeFlag.IsFirstCall() == false) return;

                lock (LockObj)
                {
                    if (Direction == FastPipeEndAttachDirection.B_UpperSide)
                    {
                        SetStreamReceiveTimeout(Timeout.Infinite);
                        SetStreamSendTimeout(Timeout.Infinite);
                    }
                }

                lock (PipeEnd._InternalAttachHandleLock)
                {
                    PipeEnd._InternalCurrentAttachHandle = null;
                }
            }
            finally { base.Dispose(disposing); }
        }
    }

    class FastPipeEndStream : FastStream
    {
        public bool AutoFlush { get; set; }
        public FastPipeEnd End { get; private set; }

        public FastPipeEndStream(FastPipeEnd end, bool autoFlush = true)
        {
            end.CheckDisconnected();

            End = end;
            AutoFlush = autoFlush;

            ReadTimeout = Timeout.Infinite;
            WriteTimeout = Timeout.Infinite;
        }

        #region Stream
        public bool IsReadyToSend => End.StreamReader.IsReadyToWrite;
        public bool IsReadyToReceive => End.StreamReader.IsReadyToRead;
        public bool IsReadyToSendTo => End.DatagramReader.IsReadyToWrite;
        public bool IsReadyToReceiveFrom => End.DatagramReader.IsReadyToRead;
        public bool IsDisconnected => End.StreamReader.IsDisconnected || End.DatagramReader.IsDisconnected;

        public void CheckDisconnect()
        {
            End.StreamReader.CheckDisconnected();
            End.DatagramReader.CheckDisconnected();
        }

        public Task WaitReadyToSendAsync(CancellationToken cancel, int timeout)
        {
            cancel.ThrowIfCancellationRequested();

            if (End.StreamWriter.IsReadyToWrite) return Task.CompletedTask;

            return End.StreamWriter.WaitForReadyToWrite(cancel, timeout);
        }

        public Task WaitReadyToReceiveAsync(CancellationToken cancel, int timeout)
        {
            cancel.ThrowIfCancellationRequested();

            if (End.StreamReader.IsReadyToRead) return Task.CompletedTask;

            return End.StreamReader.WaitForReadyToRead(cancel, timeout);
        }

        public async Task FastSendAsync(Memory<Memory<byte>> items, CancellationToken cancel = default, bool flush = true)
        {
            await WaitReadyToSendAsync(cancel, WriteTimeout);

            End.StreamWriter.EnqueueAllWithLock(items.Span);

            if (flush) FastFlush(true, false);
        }

        public async Task FastSendAsync(Memory<byte> item, CancellationToken cancel = default, bool flush = true)
        {
            await WaitReadyToSendAsync(cancel, WriteTimeout);

            lock (End.StreamWriter.LockObj)
            {
                End.StreamWriter.Enqueue(item);
            }

            if (flush) FastFlush(true, false);
        }

        public async Task SendAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancel = default)
        {
            Memory<byte> sendData = buffer.ToArray();

            await FastSendAsync(sendData, cancel);

            if (AutoFlush) FastFlush(true, false);
        }

        public void Send(ReadOnlyMemory<byte> buffer, CancellationToken cancel = default)
            => SendAsync(buffer, cancel).Wait();

        Once receiveAllAsyncRaiseExceptionFlag;

        public async Task ReceiveAllAsync(Memory<byte> buffer, CancellationToken cancel = default)
        {
            while (buffer.Length >= 1)
            {
                int r = await ReceiveAsync(buffer, cancel);
                if (r <= 0)
                {
                    End.StreamReader.CheckDisconnected();

                    if (receiveAllAsyncRaiseExceptionFlag.IsFirstCall())
                    {
                        End.StreamReader.ExceptionQueue.Raise(new FastBufferDisconnectedException());
                    }
                    else
                    {
                        End.StreamReader.ExceptionQueue.ThrowFirstExceptionIfExists();
                        throw new FastBufferDisconnectedException();
                    }
                }
                buffer.Walk(r);
            }
        }

        public async Task<Memory<byte>> ReceiveAllAsync(int size, CancellationToken cancel = default)
        {
            Memory<byte> buffer = new byte[size];
            await ReceiveAllAsync(buffer, cancel);
            return buffer;
        }

        public async Task<int> ReceiveAsync(Memory<byte> buffer, CancellationToken cancel = default)
        {
            try
            {
                LABEL_RETRY:
                await WaitReadyToReceiveAsync(cancel, ReadTimeout);

                int ret = 0;

                lock (End.StreamReader.LockObj)
                    ret = End.StreamReader.DequeueContiguousSlow(buffer);

                if (ret == 0)
                {
                    await Task.Yield();
                    goto LABEL_RETRY;
                }

                Debug.Assert(ret <= buffer.Length);

                End.StreamReader.CompleteRead();

                return ret;
            }
            catch (DisconnectedException)
            {
                return 0;
            }
        }

        public async Task<Memory<byte>> ReceiveAsync(int maxSize = int.MaxValue, CancellationToken cancel = default)
        {
            try
            {
                LABEL_RETRY:
                await WaitReadyToReceiveAsync(cancel, ReadTimeout);

                Memory<byte> ret;

                lock (End.StreamReader.LockObj)
                    ret = End.StreamReader.DequeueContiguousSlow(maxSize);

                if (ret.Length == 0)
                {
                    await Task.Yield();
                    goto LABEL_RETRY;
                }

                End.StreamReader.CompleteRead();

                return ret;
            }
            catch (DisconnectedException)
            {
                return Memory<byte>.Empty;
            }
        }

        public void ReceiveAll(Memory<byte> buffer, CancellationToken cancel = default)
            => ReceiveAllAsync(buffer, cancel).Wait();

        public Memory<byte> ReceiveAll(int size, CancellationToken cancel = default)
            => ReceiveAllAsync(size, cancel).Result;

        public int Receive(Memory<byte> buffer, CancellationToken cancel = default)
            => ReceiveAsync(buffer, cancel).Result;

        public Memory<byte> Receive(int maxSize = int.MaxValue, CancellationToken cancel = default)
            => ReceiveAsync(maxSize, cancel).Result;

        public async Task<List<Memory<byte>>> FastReceiveAsync(CancellationToken cancel = default, RefInt totalRecvSize = null)
        {
            try
            {
                LABEL_RETRY:
                await WaitReadyToReceiveAsync(cancel, ReadTimeout);

                var ret = End.StreamReader.DequeueAllWithLock(out long totalReadSize);

                if (totalRecvSize != null)
                    totalRecvSize.Set((int)totalReadSize);

                if (totalReadSize == 0)
                {
                    await Task.Yield();
                    goto LABEL_RETRY;
                }

                End.StreamReader.CompleteRead();

                return ret;
            }
            catch (DisconnectedException)
            {
                return new List<Memory<byte>>();
            }
        }

        public async Task<List<Memory<byte>>> FastPeekAsync(int maxSize = int.MaxValue, CancellationToken cancel = default, RefInt totalRecvSize = null)
        {
            LABEL_RETRY:
            CheckDisconnect();
            await WaitReadyToReceiveAsync(cancel, ReadTimeout);
            CheckDisconnect();

            long totalReadSize;
            FastBufferSegment<Memory<byte>>[] tmp;
            lock (End.StreamReader.LockObj)
            {
                tmp = End.StreamReader.GetSegmentsFast(End.StreamReader.PinHead, maxSize, out totalReadSize, true);
            }

            if (totalRecvSize != null)
                totalRecvSize.Set((int)totalReadSize);

            if (totalReadSize == 0)
            {
                await Task.Yield();
                goto LABEL_RETRY;
            }

            List<Memory<byte>> ret = new List<Memory<byte>>();
            foreach (FastBufferSegment<Memory<byte>> item in tmp)
                ret.Add(item.Item);

            return ret;
        }

        public async Task<Memory<byte>> FastPeekContiguousAsync(int maxSize = int.MaxValue, CancellationToken cancel = default)
        {
            LABEL_RETRY:
            CheckDisconnect();
            await WaitReadyToReceiveAsync(cancel, ReadTimeout);
            CheckDisconnect();

            Memory<byte> ret;

            lock (End.StreamReader.LockObj)
            {
                ret = End.StreamReader.GetContiguous(End.StreamReader.PinHead, maxSize, true);
            }

            if (ret.Length == 0)
            {
                await Task.Yield();
                goto LABEL_RETRY;
            }

            return ret;
        }

        public async Task<Memory<byte>> PeekAsync(int maxSize = int.MaxValue, CancellationToken cancel = default)
            => (await FastPeekContiguousAsync(maxSize, cancel)).ToArray();

        public async Task<int> PeekAsync(Memory<byte> buffer, CancellationToken cancel = default)
        {
            var tmp = await PeekAsync(buffer.Length, cancel);
            tmp.CopyTo(buffer);
            return tmp.Length;
        }

        #endregion

        #region Datagram
        public Task WaitReadyToSendToAsync(CancellationToken cancel, int timeout)
        {
            cancel.ThrowIfCancellationRequested();

            if (End.DatagramWriter.IsReadyToWrite) return Task.CompletedTask;

            return End.DatagramWriter.WaitForReadyToWrite(cancel, timeout);
        }

        public Task WaitReadyToReceiveFromAsync(CancellationToken cancel, int timeout)
        {
            cancel.ThrowIfCancellationRequested();

            if (End.DatagramReader.IsReadyToRead) return Task.CompletedTask;

            return End.DatagramReader.WaitForReadyToRead(cancel, timeout);
        }

        public async Task FastSendToAsync(Memory<Datagram> items, CancellationToken cancel = default, bool flush = true)
        {
            await WaitReadyToSendToAsync(cancel, WriteTimeout);

            End.DatagramWriter.EnqueueAllWithLock(items.Span);

            if (flush) FastFlush(false, true);
        }

        public async Task FastSendToAsync(Datagram item, CancellationToken cancel = default, bool flush = true)
        {
            await WaitReadyToSendToAsync(cancel, WriteTimeout);

            lock (End.StreamWriter.LockObj)
            {
                End.DatagramWriter.Enqueue(item);
            }

            if (flush) FastFlush(false, true);
        }

        public async Task SendToAsync(ReadOnlyMemory<byte> buffer, EndPoint remoteEndPoint, CancellationToken cancel = default)
        {
            Datagram sendData = new Datagram(buffer.Span.ToArray(), remoteEndPoint);

            await FastSendToAsync(sendData, cancel);

            if (AutoFlush) FastFlush(false, true);
        }

        public void SendTo(ReadOnlyMemory<byte> buffer, EndPoint remoteEndPoint, CancellationToken cancel = default)
            => SendToAsync(buffer, remoteEndPoint, cancel).Wait();

        public async Task<List<Datagram>> FastReceiveFromAsync(CancellationToken cancel = default)
        {
            LABEL_RETRY:
            await WaitReadyToReceiveFromAsync(cancel, ReadTimeout);

            var ret = End.DatagramReader.DequeueAllWithLock(out long totalReadSize);
            if (totalReadSize == 0)
            {
                await Task.Yield();
                goto LABEL_RETRY;
            }

            End.DatagramReader.CompleteRead();

            return ret;
        }

        public async Task<Datagram> ReceiveFromAsync(CancellationToken cancel = default)
        {
            LABEL_RETRY:
            await WaitReadyToReceiveFromAsync(cancel, ReadTimeout);

            List<Datagram> dataList;

            long totalReadSize;

            lock (End.DatagramReader.LockObj)
            {
                dataList = End.DatagramReader.Dequeue(1, out totalReadSize);
            }

            if (totalReadSize == 0)
            {
                await Task.Yield();
                goto LABEL_RETRY;
            }

            Debug.Assert(dataList.Count == 1);

            End.DatagramReader.CompleteRead();

            return dataList[0];
        }

        public async Task<PalSocketReceiveFromResult> ReceiveFromAsync(Memory<byte> buffer, CancellationToken cancel = default)
        {
            var datagram = await ReceiveFromAsync(cancel);
            datagram.Data.CopyTo(buffer);

            PalSocketReceiveFromResult ret = new PalSocketReceiveFromResult();
            ret.ReceivedBytes = datagram.Data.Length;
            ret.RemoteEndPoint = datagram.EndPoint;
            return ret;
        }

        public int ReceiveFrom(Memory<byte> buffer, out EndPoint remoteEndPoint, CancellationToken cancel = default)
        {
            PalSocketReceiveFromResult r = ReceiveFromAsync(buffer, cancel).Result;

            remoteEndPoint = r.RemoteEndPoint;

            return r.ReceivedBytes;
        }

        #endregion

        public void FastFlush(bool stream = true, bool datagram = true)
        {
            if (stream)
                End.StreamWriter.CompleteWrite();

            if (datagram)
                End.DatagramWriter.CompleteWrite();
        }

        public void Disconnect() => End.Disconnect();

        public override int ReadTimeout { get; set; }
        public override int WriteTimeout { get; set; }

        public override bool DataAvailable => IsReadyToReceive;

        public virtual void Flush() => FastFlush();

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            Flush();
            return Task.CompletedTask;
        }

        public virtual Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => SendAsync(buffer.AsReadOnlyMemory(offset, count), cancellationToken);

        public virtual Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => ReceiveAsync(buffer.AsMemory(offset, count), cancellationToken);

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => await ReceiveAsync(buffer, cancellationToken);

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            => await this.SendAsync(buffer, cancellationToken);

        Once DisposeFlag;
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!disposing || DisposeFlag.IsFirstCall() == false) return;
                Disconnect();
            }
            finally { base.Dispose(disposing); }
        }
    }

    class FastPipeNonblockStateHelper
    {
        byte[] LastState = new byte[0];

        public FastPipeNonblockStateHelper() { }
        public FastPipeNonblockStateHelper(IFastBufferState reader, IFastBufferState writer, CancellationToken cancel = default) : this()
        {
            AddWatchReader(reader);
            AddWatchWriter(writer);
            AddCancel(cancel);
        }

        List<IFastBufferState> ReaderList = new List<IFastBufferState>();
        List<IFastBufferState> WriterList = new List<IFastBufferState>();
        List<AsyncAutoResetEvent> WaitEventList = new List<AsyncAutoResetEvent>();
        List<CancellationToken> WaitCancelList = new List<CancellationToken>();

        public void AddWatchReader(IFastBufferState obj)
        {
            if (ReaderList.Contains(obj) == false)
                ReaderList.Add(obj);
            AddEvent(obj.EventReadReady);
        }

        public void AddWatchWriter(IFastBufferState obj)
        {
            if (WriterList.Contains(obj) == false)
                WriterList.Add(obj);
            AddEvent(obj.EventWriteReady);
        }

        public void AddEvent(AsyncAutoResetEvent ev) => AddEvents(new AsyncAutoResetEvent[] { ev });
        public void AddEvents(params AsyncAutoResetEvent[] events)
        {
            foreach (var ev in events)
                if (WaitEventList.Contains(ev) == false)
                    WaitEventList.Add(ev);
        }

        public void AddCancel(CancellationToken c) => AddCancels(new CancellationToken[] { c });
        public void AddCancels(params CancellationToken[] cancels)
        {
            foreach (var cancel in cancels)
                if (cancel.CanBeCanceled)
                    if (WaitCancelList.Contains(cancel) == false)
                        WaitCancelList.Add(cancel);
        }

        public byte[] SnapshotState(long salt = 0)
        {
            SpanBuffer<byte> ret = new SpanBuffer<byte>();
            ret.WriteSInt64(salt);
            foreach (var s in ReaderList)
            {
                lock (s.LockObj)
                {
                    ret.WriteUInt8((byte)(s.IsReadyToRead ? 1 : 0));
                    ret.WriteSInt64(s.PinTail);
                }
            }
            foreach (var s in WriterList)
            {
                lock (s.LockObj)
                {
                    ret.WriteUInt8((byte)(s.IsReadyToWrite ? 1 : 0));
                    ret.WriteSInt64(s.PinHead);
                }
            }
            return ret.Span.ToArray();
        }

        public bool IsStateChanged(int salt = 0)
        {
            byte[] newState = SnapshotState(salt);
            if (LastState.SequenceEqual(newState))
                return false;
            LastState = newState;
            return true;
        }

        public async Task<bool> WaitIfNothingChanged(int timeout = Timeout.Infinite, int salt = 0)
        {
            timeout = WebSocketHelper.GetMinTimeout(timeout, FastPipeGlobalConfig.PollingTimeout);
            if (timeout == 0) return false;
            if (IsStateChanged(salt)) return false;

            await WebSocketHelper.WaitObjectsAsync(
                cancels: WaitCancelList.ToArray(),
                events: WaitEventList.ToArray(),
                timeout: timeout);

            return true;
        }
    }

    [Flags]
    enum PipeSupportedDataTypes
    {
        Stream = 1,
        Datagram = 2,
    }

    abstract class FastPipeEndAsyncObjectWrapperBase : AsyncCleanupable
    {
        public CancelWatcher CancelWatcher { get; }
        public FastPipeEnd PipeEnd { get; }
        public abstract PipeSupportedDataTypes SupportedDataTypes { get; }
        Task MainLoopTask = Task.CompletedTask;

        public ExceptionQueue ExceptionQueue { get => PipeEnd.ExceptionQueue; }
        public LayerInfo LayerInfo { get => PipeEnd.LayerInfo; }

        public FastPipeEndAsyncObjectWrapperBase(AsyncCleanuperLady lady, FastPipeEnd pipeEnd, CancellationToken cancel = default)
            : base(lady)
        {
            PipeEnd = pipeEnd;
            CancelWatcher = new CancelWatcher(Lady, cancel);
        }

        public override async Task _CleanupAsyncInternal()
        {
            try
            {
                await MainLoopTask.TryWaitAsync(true);

                await CancelWatcher.AsyncCleanuper;
            }
            finally { await base._CleanupAsyncInternal(); }
        }

        Once ConnectedFlag;
        protected void BaseStart()
        {
            if (ConnectedFlag.IsFirstCall())
                MainLoopTask = MainLoopAsync();
        }

        async Task MainLoopAsync()
        {
            try
            {
                List<Task> tasks = new List<Task>();

                if (SupportedDataTypes.Bit(PipeSupportedDataTypes.Stream))
                {
                    tasks.Add(StreamReadFromPipeLoopAsync());
                    tasks.Add(StreamWriteToPipeLoopAsync());
                }

                if (SupportedDataTypes.Bit(PipeSupportedDataTypes.Datagram))
                {
                    tasks.Add(DatagramReadFromPipeLoopAsync());
                    tasks.Add(DatagramWriteToPipeLoopAsync());
                }

                await Task.WhenAll(tasks.ToArray());
            }
            catch (Exception ex)
            {
                Disconnect(ex);
            }
            finally
            {
                Disconnect();
            }
        }

        List<Action> OnDisconnectedList = new List<Action>();
        public void AddOnDisconnected(Action proc) => OnDisconnectedList.Add(proc);

        protected abstract Task StreamWriteToObject(FastStreamFifo fifo, CancellationToken cancel);
        protected abstract Task StreamReadFromObject(FastStreamFifo fifo, CancellationToken cancel);

        protected abstract Task DatagramWriteToObject(FastDatagramFifo fifo, CancellationToken cancel);
        protected abstract Task DatagramReadFromObject(FastDatagramFifo fifo, CancellationToken cancel);

        async Task StreamReadFromPipeLoopAsync()
        {
            using (LeakChecker.Enter())
            {
                try
                {
                    var reader = PipeEnd.StreamReader;
                    while (true)
                    {
                        bool stateChanged;
                        do
                        {
                            stateChanged = false;

                            CancelWatcher.CancelToken.ThrowIfCancellationRequested();

                            while (reader.IsReadyToRead)
                            {
                                await StreamWriteToObject(reader, CancelWatcher.CancelToken);
                                stateChanged = true;
                            }
                        }
                        while (stateChanged);

                        await WebSocketHelper.WaitObjectsAsync(
                            events: new AsyncAutoResetEvent[] { reader.EventReadReady },
                            cancels: new CancellationToken[] { CancelWatcher.CancelToken },
                            timeout: FastPipeGlobalConfig.PollingTimeout
                            );
                    }
                }
                catch (Exception ex)
                {
                    ExceptionQueue.Raise(ex);
                }
                finally
                {
                    PipeEnd.Disconnect();
                    Disconnect();
                }
            }
        }

        async Task StreamWriteToPipeLoopAsync()
        {
            using (LeakChecker.Enter())
            {
                try
                {
                    var writer = PipeEnd.StreamWriter;
                    while (true)
                    {
                        bool stateChanged;
                        do
                        {
                            stateChanged = false;

                            CancelWatcher.CancelToken.ThrowIfCancellationRequested();

                            if (writer.IsReadyToWrite)
                            {
                                long lastTail = writer.PinTail;
                                await StreamReadFromObject(writer, CancelWatcher.CancelToken);
                                if (writer.PinTail != lastTail)
                                {
                                    stateChanged = true;
                                }
                            }

                        }
                        while (stateChanged);

                        await WebSocketHelper.WaitObjectsAsync(
                            events: new AsyncAutoResetEvent[] { writer.EventWriteReady },
                            cancels: new CancellationToken[] { CancelWatcher.CancelToken },
                            timeout: FastPipeGlobalConfig.PollingTimeout
                            );
                    }
                }
                catch (Exception ex)
                {
                    ExceptionQueue.Raise(ex);
                }
                finally
                {
                    PipeEnd.Disconnect();
                }
            }
        }

        async Task DatagramReadFromPipeLoopAsync()
        {
            using (LeakChecker.Enter())
            {
                try
                {
                    var reader = PipeEnd.DatagramReader;
                    while (true)
                    {
                        bool stateChanged;
                        do
                        {
                            stateChanged = false;

                            CancelWatcher.CancelToken.ThrowIfCancellationRequested();

                            while (reader.IsReadyToRead)
                            {
                                await DatagramWriteToObject(reader, CancelWatcher.CancelToken);
                                stateChanged = true;
                            }
                        }
                        while (stateChanged);

                        await WebSocketHelper.WaitObjectsAsync(
                            events: new AsyncAutoResetEvent[] { reader.EventReadReady },
                            cancels: new CancellationToken[] { CancelWatcher.CancelToken },
                            timeout: FastPipeGlobalConfig.PollingTimeout
                            );
                    }
                }
                catch (Exception ex)
                {
                    ExceptionQueue.Raise(ex);
                }
                finally
                {
                    PipeEnd.Disconnect();
                    Disconnect();
                }
            }
        }

        async Task DatagramWriteToPipeLoopAsync()
        {
            using (LeakChecker.Enter())
            {
                try
                {
                    var writer = PipeEnd.DatagramWriter;
                    while (true)
                    {
                        bool stateChanged;
                        do
                        {
                            stateChanged = false;

                            CancelWatcher.CancelToken.ThrowIfCancellationRequested();

                            if (writer.IsReadyToWrite)
                            {
                                long lastTail = writer.PinTail;
                                await DatagramReadFromObject(writer, CancelWatcher.CancelToken);
                                if (writer.PinTail != lastTail)
                                {
                                    stateChanged = true;
                                }
                            }

                        }
                        while (stateChanged);

                        await WebSocketHelper.WaitObjectsAsync(
                            events: new AsyncAutoResetEvent[] { writer.EventWriteReady },
                            cancels: new CancellationToken[] { CancelWatcher.CancelToken },
                            timeout: FastPipeGlobalConfig.PollingTimeout
                            );
                    }
                }
                catch (Exception ex)
                {
                    ExceptionQueue.Raise(ex);
                }
                finally
                {
                    PipeEnd.Disconnect();
                }
            }
        }

        Once DisconnectedFlag;
        public void Disconnect(Exception ex = null)
        {
            if (DisconnectedFlag.IsFirstCall())
            {
                this.PipeEnd.Disconnect(ex);
                CancelWatcher.Cancel();

                foreach (var proc in OnDisconnectedList) try { proc(); } catch { };
            }
        }

        Once DisposeFlag;
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!disposing || DisposeFlag.IsFirstCall() == false) return;

                Disconnect();
                CancelWatcher.DisposeSafe();
            }
            finally { base.Dispose(disposing); }
        }
    }

    class FastPipeEndSocketWrapper : FastPipeEndAsyncObjectWrapperBase
    {
        public PalSocket Socket { get; }
        public int RecvTmpBufferSize { get; private set; }
        public override PipeSupportedDataTypes SupportedDataTypes { get; }

        public FastPipeEndSocketWrapper(AsyncCleanuperLady lady, FastPipeEnd pipeEnd, PalSocket socket, CancellationToken cancel = default) : base(lady, pipeEnd, cancel)
        {
            this.Socket = socket;
            SupportedDataTypes = (Socket.SocketType == SocketType.Stream) ? PipeSupportedDataTypes.Stream : PipeSupportedDataTypes.Datagram;
            if (Socket.SocketType == SocketType.Stream)
            {
                Socket.LingerTime.Value = 0;
                Socket.NoDelay.Value = false;
            }
            this.AddOnDisconnected(() => Socket.DisposeSafe());

            this.BaseStart();
        }

        protected override async Task StreamWriteToObject(FastStreamFifo fifo, CancellationToken cancel)
        {
            if (SupportedDataTypes.Bit(PipeSupportedDataTypes.Stream) == false) throw new NotSupportedException();

            List<Memory<byte>> sendArray;

            sendArray = fifo.DequeueAllWithLock(out long totalReadSize);
            fifo.CompleteRead();

            await WebSocketHelper.DoAsyncWithTimeout(
                async c =>
                {
                    int ret = await Socket.SendAsync(sendArray);
                    return 0;
                },
                cancel: cancel);
        }

        FastMemoryAllocator<byte> FastMemoryAllocatorForStream = new FastMemoryAllocator<byte>();

        AsyncBulkReceiver<Memory<byte>, FastPipeEndSocketWrapper> StreamBulkReceiver = new AsyncBulkReceiver<Memory<byte>, FastPipeEndSocketWrapper>(async me =>
        {
            if (me.RecvTmpBufferSize == 0)
            {
                int i = me.Socket.ReceiveBufferSize;
                if (i <= 0) i = 65536;
                me.RecvTmpBufferSize = Math.Min(i, FastPipeGlobalConfig.MaxStreamBufferLength);
            }

            Memory<byte> tmp = me.FastMemoryAllocatorForStream.Reserve(me.RecvTmpBufferSize);
            int r = await me.Socket.ReceiveAsync(tmp);
            if (r < 0) throw new SocketDisconnectedException();
            me.FastMemoryAllocatorForStream.Commit(ref tmp, r);
            if (r == 0) return new ValueOrClosed<Memory<byte>>();
            return new ValueOrClosed<Memory<byte>>(tmp);
        });

        protected override async Task StreamReadFromObject(FastStreamFifo fifo, CancellationToken cancel)
        {
            if (SupportedDataTypes.Bit(PipeSupportedDataTypes.Stream) == false) throw new NotSupportedException();

            Memory<byte>[] recvList = await StreamBulkReceiver.Recv(cancel, this);

            if (recvList == null)
            {
                // disconnected
                fifo.Disconnect();
                return;
            }

            fifo.EnqueueAllWithLock(recvList);

            fifo.CompleteWrite();
        }

        protected override async Task DatagramWriteToObject(FastDatagramFifo fifo, CancellationToken cancel)
        {
            if (SupportedDataTypes.Bit(PipeSupportedDataTypes.Datagram) == false) throw new NotSupportedException();

            List<Datagram> sendList;

            sendList = fifo.DequeueAllWithLock(out _);
            fifo.CompleteRead();

            await WebSocketHelper.DoAsyncWithTimeout(
                async c =>
                {
                    foreach (Datagram data in sendList)
                    {
                        cancel.ThrowIfCancellationRequested();
                        await Socket.SendToAsync(data.Data.AsSegment(), data.EndPoint);
                    }
                    return 0;
                },
                cancel: cancel);
        }

        FastMemoryAllocator<byte> FastMemoryAllocatorForDatagram = new FastMemoryAllocator<byte>();

        AsyncBulkReceiver<Datagram, FastPipeEndSocketWrapper> DatagramBulkReceiver = new AsyncBulkReceiver<Datagram, FastPipeEndSocketWrapper>(async me =>
        {
            Memory<byte> tmp = me.FastMemoryAllocatorForDatagram.Reserve(65536);

            var ret = await me.Socket.ReceiveFromAsync(tmp);

            me.FastMemoryAllocatorForDatagram.Commit(ref tmp, ret.ReceivedBytes);

            Datagram pkt = new Datagram(tmp, ret.RemoteEndPoint);
            return new ValueOrClosed<Datagram>(pkt);
        });

        protected override async Task DatagramReadFromObject(FastDatagramFifo fifo, CancellationToken cancel)
        {
            if (SupportedDataTypes.Bit(PipeSupportedDataTypes.Datagram) == false) throw new NotSupportedException();

            Datagram[] pkts = await DatagramBulkReceiver.Recv(cancel, this);

            fifo.EnqueueAllWithLock(pkts);

            fifo.CompleteWrite();
        }

        Once DisposeFlag;
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!disposing || DisposeFlag.IsFirstCall() == false) return;
                Socket.DisposeSafe();
            }
            finally { base.Dispose(disposing); }
        }
    }

    class FastPipeEndStreamWrapper : FastPipeEndAsyncObjectWrapperBase
    {
        public FastStream Stream { get; }
        public int RecvTmpBufferSize { get; private set; }
        public const int SendTmpBufferSize = 65536;
        public override PipeSupportedDataTypes SupportedDataTypes { get; }

        Memory<byte> SendTmpBuffer = new byte[SendTmpBufferSize];

        //static bool UseDontLingerOption = true;

        public FastPipeEndStreamWrapper(AsyncCleanuperLady lady, FastPipeEnd pipeEnd, FastStream stream, CancellationToken cancel = default) : base(lady, pipeEnd, cancel)
        {
            this.Stream = stream;
            SupportedDataTypes = PipeSupportedDataTypes.Stream;

            Stream.ReadTimeout = Stream.WriteTimeout = Timeout.Infinite;
            this.AddOnDisconnected(() => Stream.DisposeSafe());

            this.BaseStart();
        }

        protected override async Task StreamWriteToObject(FastStreamFifo fifo, CancellationToken cancel)
        {
            if (SupportedDataTypes.Bit(PipeSupportedDataTypes.Stream) == false) throw new NotSupportedException();

            await WebSocketHelper.DoAsyncWithTimeout(
                async c =>
                {
                    while (true)
                    {
                        int size = fifo.DequeueContiguousSlow(SendTmpBuffer, SendTmpBuffer.Length);
                        if (size == 0)
                            break;

                        await Stream.WriteAsync(SendTmpBuffer.Slice(0, size));
                    }

                    return 0;
                },
                cancel: cancel);
        }

        FastMemoryAllocator<byte> FastMemoryAllocatorForStream = new FastMemoryAllocator<byte>();

        AsyncBulkReceiver<Memory<byte>, FastPipeEndStreamWrapper> StreamBulkReceiver = new AsyncBulkReceiver<Memory<byte>, FastPipeEndStreamWrapper>(async me =>
        {
            if (me.RecvTmpBufferSize == 0)
            {
                int i = 65536;
                me.RecvTmpBufferSize = Math.Min(i, FastPipeGlobalConfig.MaxStreamBufferLength);
            }

            Memory<byte> tmp = me.FastMemoryAllocatorForStream.Reserve(me.RecvTmpBufferSize);
            int r = await me.Stream.ReadAsync(tmp);
            if (r < 0) throw new BaseStreamDisconnectedException();
            me.FastMemoryAllocatorForStream.Commit(ref tmp, r);
            if (r == 0) return new ValueOrClosed<Memory<byte>>();
            return new ValueOrClosed<Memory<byte>>(tmp);
        });

        protected override async Task StreamReadFromObject(FastStreamFifo fifo, CancellationToken cancel)
        {
            if (SupportedDataTypes.Bit(PipeSupportedDataTypes.Stream) == false) throw new NotSupportedException();

            Memory<byte>[] recvList = await StreamBulkReceiver.Recv(cancel, this);

            if (recvList == null)
            {
                // disconnected
                fifo.Disconnect();
                return;
            }

            fifo.EnqueueAllWithLock(recvList);

            fifo.CompleteWrite();
        }

        protected override Task DatagramWriteToObject(FastDatagramFifo fifo, CancellationToken cancel)
            => throw new NotSupportedException();

        protected override Task DatagramReadFromObject(FastDatagramFifo fifo, CancellationToken cancel)
            => throw new NotSupportedException();

        Once DisposeFlag;
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!disposing || DisposeFlag.IsFirstCall() == false) return;
                Stream.DisposeSafe();
            }
            finally { base.Dispose(disposing); }
        }
    }

    interface IFastStream
    {
        ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancel = default);
        ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancel = default);
        int ReadTimeout { get; set; }
        int WriteTimeout { get; set; }
        bool DataAvailable { get; }
        Task FlushAsync(CancellationToken cancel = default);
    }

    abstract class FastStream : IDisposable, IFastStream
    {
        public abstract int ReadTimeout { get; set; }
        public abstract int WriteTimeout { get; set; }
        public abstract bool DataAvailable { get; }

        public abstract ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancel = default);
        public abstract ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancel = default);
        public abstract Task FlushAsync(CancellationToken cancel = default);

        public void Dispose() => Dispose(true);
        protected virtual void Dispose(bool disposing) { }

        public FastStreamToPalNetworkStream GetPalNetworkStream()
            => FastStreamToPalNetworkStream.CreateFromFastStream(this);
    }

    abstract class FastStackOptionsBase { }

    abstract class FastStackBase : AsyncCleanupable
    {
        public CancelWatcher CancelWatcher { get; }

        public CancellationToken GrandCancel { get => CancelWatcher.CancelToken; }

        public FastStackOptionsBase StackOptions { get; }

        public FastStackBase(AsyncCleanuperLady lady, FastStackOptionsBase options, CancellationToken cancel = default) :
            base(lady)
        {
            try
            {
                StackOptions = options;

                CancelWatcher = new CancelWatcher(Lady, cancel);
            }
            catch
            {
                Lady.DisposeAllSafe();
                throw;
            }
        }
    }

    abstract class FastAppStubOptionsBase : FastStackOptionsBase { }

    abstract class FastAppStubBase : FastStackBase
    {
        protected FastPipeEnd Lower { get; }
        protected FastAttachHandle LowerAttach { get; private set; }

        CriticalSection LockObj = new CriticalSection();

        public FastAppStubOptionsBase TopOptions { get; }

        public FastAppStubBase(AsyncCleanuperLady lady, FastPipeEnd lower, FastAppStubOptionsBase options, CancellationToken cancel = default)
            : base(lady, options, cancel)
        {
            try
            {
                TopOptions = options;
                Lower = lower;

                LowerAttach = Lower.Attach(this.Lady, FastPipeEndAttachDirection.B_UpperSide);
            }
            catch
            {
                Lady.DisposeAllSafe();
                throw;
            }
        }

        public virtual void Disconnect(Exception ex)
        {
            Lower.Disconnect(ex);
        }
    }

    class FastAppStubOptions : FastAppStubOptionsBase { }

    class FastAppStub : FastAppStubBase
    {
        public FastAppStub(AsyncCleanuperLady lady, FastPipeEnd lower, CancellationToken cancel = default, FastAppStubOptions options = null)
            : base(lady, lower, options ?? new FastAppStubOptions(), cancel)
        {
        }

        CriticalSection LockObj = new CriticalSection();

        FastPipeEndStream StreamCache = null;

        public FastPipeEndStream GetStream(bool autoFlash = true)
        {
            lock (LockObj)
            {
                if (StreamCache == null)
                    StreamCache = AttachHandle.GetStream(autoFlash);

                return StreamCache;
            }
        }

        public FastPipeEnd GetPipeEnd()
        {
            Lower.CheckDisconnected();

            return Lower;
        }

        public FastAttachHandle AttachHandle
        {
            get
            {
                Lower.CheckDisconnected();
                return this.LowerAttach;
            }
        }

        Once DisposeFlag;
        protected override void Dispose(bool disposing)
        {
            try
            {
                if (!disposing || DisposeFlag.IsFirstCall() == false) return;
                StreamCache.DisposeSafe();
            }
            finally { base.Dispose(disposing); }
        }
    }

    abstract class FastProtocolOptionsBase : FastStackOptionsBase { }

    abstract class FastProtocolBase : FastStackBase
    {
        protected FastPipeEnd Upper { get; }

        protected internal FastPipeEnd _InternalUpper { get => Upper; }

        protected FastAttachHandle UpperAttach { get; private set; }

        public FastProtocolOptionsBase ProtocolOptions { get; }

        public FastProtocolBase(AsyncCleanuperLady lady, FastPipeEnd upper, FastProtocolOptionsBase options, CancellationToken cancel = default)
            : base(lady, options, cancel)
        {
            try
            {
                if (upper == null)
                {
                    upper = FastPipeEnd.NewFastPipeAndGetOneSide(FastPipeEndSide.A_LowerSide, Lady, cancel);
                    Lady.Add(upper.Pipe);
                }

                ProtocolOptions = options;
                Upper = upper;

                UpperAttach = Upper.Attach(this.Lady, FastPipeEndAttachDirection.A_LowerSide);
            }
            catch
            {
                Lady.DisposeAllSafe();
                throw;
            }
        }

        public virtual void Disconnect(Exception ex = null)
        {
            Upper.Disconnect(ex);
        }
    }

    abstract class FastBottomProtocolOptionsBase : FastProtocolOptionsBase { }

    abstract class FastBottomProtocolStubBase : FastProtocolBase
    {
        public FastBottomProtocolStubBase(AsyncCleanuperLady lady, FastPipeEnd upper, FastProtocolOptionsBase options, CancellationToken cancel = default) : base(lady, upper, options, cancel) { }
    }

    abstract class FastTcpProtocolOptionsBase : FastBottomProtocolOptionsBase
    {
        public FastDnsClientStub DnsClient { get; set; }
    }

    abstract class FastTcpProtocolStubBase : FastBottomProtocolStubBase
    {
        public const int DefaultTcpConnectTimeout = 15 * 1000;

        FastTcpProtocolOptionsBase Options { get; }

        public FastTcpProtocolStubBase(AsyncCleanuperLady lady, FastPipeEnd upper, FastTcpProtocolOptionsBase options, CancellationToken cancel = default) : base(lady, upper, options, cancel)
        {
            Options = options;
        }

        protected abstract Task ConnectMainAsync(IPEndPoint remoteEndPoint, int connectTimeout = DefaultTcpConnectTimeout);
        protected abstract void ListenMain(IPEndPoint localEndPoint);
        protected abstract Task<FastTcpProtocolStubBase> AcceptMainAsync(AsyncCleanuperLady lady, CancellationToken cancelForNewSocket = default);

        public bool IsConnected { get; private set; }
        public bool IsListening { get; private set; }
        public bool IsServerMode { get; private set; }

        AsyncLock ConnectLock = new AsyncLock();

        public async Task<FastSock> ConnectAsync(IPEndPoint remoteEndPoint, int connectTimeout = DefaultTcpConnectTimeout)
        {
            using (await ConnectLock.LockWithAwait())
            {
                if (IsConnected) throw new ApplicationException("Already connected.");
                if (IsListening) throw new ApplicationException("Already listening.");

                await ConnectMainAsync(remoteEndPoint, connectTimeout);

                IsConnected = true;
                IsServerMode = false;

                return new FastSock(this.Lady, this);
            }
        }

        public Task<FastSock> ConnectAsync(IPAddress ip, int port, CancellationToken cancel = default, int connectTimeout = FastTcpProtocolStubBase.DefaultTcpConnectTimeout)
            => ConnectAsync(new IPEndPoint(ip, port), connectTimeout);

        public async Task<FastSock> ConnectAsync(string host, int port, AddressFamily? addressFamily = null, int connectTimeout = FastTcpProtocolStubBase.DefaultTcpConnectTimeout)
            => await ConnectAsync(await Options.DnsClient.GetIPFromHostName(host, addressFamily, GrandCancel, connectTimeout), port, default, connectTimeout);

        object ListenLock = new object();

        public void Listen(IPEndPoint localEndPoint)
        {
            lock (ListenLock)
            {
                if (IsConnected) throw new ApplicationException("Already connected.");
                if (IsListening) throw new ApplicationException("Already listening.");

                ListenMain(localEndPoint);

                IsListening = true;
                IsServerMode = true;
            }
        }

        public async Task<FastSock> AcceptAsync(AsyncCleanuperLady lady, CancellationToken cancelForNewSocket = default)
        {
            if (IsListening == false) throw new ApplicationException("Not listening.");

            return new FastSock(lady, await AcceptMainAsync(lady, cancelForNewSocket));
        }
    }

    class FastPalTcpProtocolOptions : FastTcpProtocolOptionsBase
    {
        public FastPalTcpProtocolOptions()
        {
            this.DnsClient = FastPalDnsClient.Shared;
        }
    }

    class FastPalTcpProtocolStub : FastTcpProtocolStubBase
    {
        public class LayerInfo : LayerInfoBase, ILayerInfoTcpEndPoint
        {
            public int LocalPort { get; set; }
            public int RemotePort { get; set; }
            public IPAddress LocalIPAddress { get; set; }
            public IPAddress RemoteIPAddress { get; set; }
        }

        public FastPalTcpProtocolStub(AsyncCleanuperLady lady, FastPipeEnd upper = null, FastPalTcpProtocolOptions options = null, CancellationToken cancel = default)
            : base(lady, upper, options ?? new FastPalTcpProtocolOptions(), cancel)
        {
        }

        public virtual void FromSocket(PalSocket s)
        {
            AsyncCleanuperLady lady = new AsyncCleanuperLady();

            try
            {
                var socketWrapper = new FastPipeEndSocketWrapper(lady, Upper, s, GrandCancel);

                UpperAttach.SetLayerInfo(new LayerInfo()
                {
                    LocalPort = ((IPEndPoint)s.LocalEndPoint).Port,
                    LocalIPAddress = ((IPEndPoint)s.LocalEndPoint).Address,
                    RemotePort = ((IPEndPoint)s.RemoteEndPoint).Port,
                    RemoteIPAddress = ((IPEndPoint)s.RemoteEndPoint).Address,
                }, this);

                this.Lady.MergeFrom(lady);
            }
            catch
            {
                lady.DisposeAllSafe();
                throw;
            }
        }

        protected override async Task ConnectMainAsync(IPEndPoint remoteEndPoint, int connectTimeout = FastTcpProtocolStubBase.DefaultTcpConnectTimeout)
        {
            AsyncCleanuperLady lady = new AsyncCleanuperLady();

            try
            {
                if (!(remoteEndPoint.AddressFamily == AddressFamily.InterNetwork || remoteEndPoint.AddressFamily == AddressFamily.InterNetworkV6))
                    throw new ArgumentException("RemoteEndPoint.AddressFamily");

                PalSocket s = new PalSocket(remoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp).AddToLady(lady);

                await WebSocketHelper.DoAsyncWithTimeout(async localCancel =>
                {
                    await s.ConnectAsync(remoteEndPoint);
                    return 0;
                },
                cancelProc: () => s.DisposeSafe(),
                timeout: connectTimeout,
                cancel: GrandCancel);

                FromSocket(s);

                this.Lady.MergeFrom(lady);
            }
            catch
            {
                await lady;
                throw;
            }
        }

        PalSocket ListeningSocket = null;

        protected override void ListenMain(IPEndPoint localEndPoint)
        {
            AsyncCleanuperLady lady = new AsyncCleanuperLady();

            try
            {
                if (!(localEndPoint.AddressFamily == AddressFamily.InterNetwork || localEndPoint.AddressFamily == AddressFamily.InterNetworkV6))
                    throw new ArgumentException("RemoteEndPoint.AddressFamily");

                PalSocket s = new PalSocket(localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp).AddToLady(lady);

                s.Bind(localEndPoint);

                s.Listen(int.MaxValue);

                ListeningSocket = s;

                this.Lady.MergeFrom(lady);
            }
            catch
            {
                lady.DisposeAllSafe();
                throw;
            }
        }

        protected override async Task<FastTcpProtocolStubBase> AcceptMainAsync(AsyncCleanuperLady lady, CancellationToken cancelForNewSocket = default)
        {
            using (CancelWatcher.EventList.RegisterCallbackWithUsing((caller, type, state) => ListeningSocket.DisposeSafe()))
            {
                PalSocket newSocket = await ListeningSocket.AcceptAsync();

                var newStub = new FastPalTcpProtocolStub(lady, null, null, cancelForNewSocket);

                newStub.FromSocket(newSocket);

                return newStub;
            }
        }
    }

    class FastSock : AsyncCleanupable
    {
        public FastProtocolBase Stack { get; }
        public FastPipe Pipe { get; }
        public FastPipeEnd LowerEnd { get; }
        public FastPipeEnd UpperEnd { get; }
        public LayerInfo Info { get => this.LowerEnd.LayerInfo; }

        internal FastSock(AsyncCleanuperLady lady, FastProtocolBase protocolStack)
            : base(lady)
        {
            try
            {
                Stack = protocolStack;
                LowerEnd = Stack._InternalUpper;
                Pipe = LowerEnd.Pipe;
                UpperEnd = LowerEnd.CounterPart;

                Lady.Add(Stack);
                Lady.Add(Pipe);
            }
            catch
            {
                Lady.DisposeAllSafe();
                throw;
            }
        }

        public FastAppStub GetFastAppProtocolStub(CancellationToken cancel = default)
        {
            FastAppStub ret = UpperEnd.GetFastAppProtocolStub(this.Lady, cancel);

            return ret;
        }

        public void Disconnect(Exception ex = null)
        {
            Pipe.Disconnect(ex);
        }
    }

    class FastDnsClientOptions : FastStackOptionsBase { }

    abstract class FastDnsClientStub : FastStackBase
    {
        public const int DefaultDnsResolveTimeout = 5 * 1000;

        public FastDnsClientStub(AsyncCleanuperLady lady, FastDnsClientOptions options, CancellationToken cancel = default) : base(lady, options, cancel)
        {
        }

        public abstract Task<IPAddress> GetIPFromHostName(string host, AddressFamily? addressFamily = null, CancellationToken cancel = default,
            int timeout = DefaultDnsResolveTimeout);
    }

    class FastPalDnsClient : FastDnsClientStub
    {
        public static FastPalDnsClient Shared { get; }

        static FastPalDnsClient()
        {
            Shared = new FastPalDnsClient(LeakChecker.SuperGrandLady, new FastDnsClientOptions());
        }

        public FastPalDnsClient(AsyncCleanuperLady lady, FastDnsClientOptions options, CancellationToken cancel = default) : base(lady, options, cancel)
        {
        }

        public override async Task<IPAddress> GetIPFromHostName(string host, AddressFamily? addressFamily = null, CancellationToken cancel = default,
            int timeout = FastDnsClientStub.DefaultDnsResolveTimeout)
        {
            if (IPAddress.TryParse(host, out IPAddress ip))
            {
                if (addressFamily != null && ip.AddressFamily != addressFamily)
                    throw new ArgumentException("ip.AddressFamily != addressFamily");
            }
            else
            {
                ip = (await PalDns.GetHostAddressesAsync(host, timeout, cancel))
                        .Where(x => x.AddressFamily == AddressFamily.InterNetwork || x.AddressFamily == AddressFamily.InterNetworkV6)
                        .Where(x => addressFamily == null || x.AddressFamily == addressFamily).First();
            }

            return ip;
        }
    }

    abstract class FastSockBase : AsyncCleanupable
    {
        public FastPipe Pipe { get; }
        FastBottomProtocolStubBase Stub;

        public FastPipeEnd UpperSidePipeEnd { get => Pipe.B_UpperSide; }

        public FastAppStub GetFastAppProtocolStub(CancellationToken cancel = default)
            => this.UpperSidePipeEnd.GetFastAppProtocolStub(this.Lady, cancel);

        public FastSockBase(AsyncCleanuperLady lady, FastPipe pipe, FastBottomProtocolStubBase stub)
            : base(lady)
        {
            try
            {
                this.Pipe = pipe.AddToLady(this);
                this.Stub = stub.AddToLady(this);
            }
            catch
            {
                Lady.DisposeAllSafe();
                throw;
            }
        }
    }

    abstract class FastMiddleProtocolOptionsBase : FastProtocolOptionsBase
    {
        public int LowerReceiveTimeoutOnInit { get; set; } = 5 * 1000;
        public int LowerSendTimeoutOnInit { get; set; } = 60 * 1000;

        public int LowerReceiveTimeoutAfterInit { get; set; } = Timeout.Infinite;
        public int LowerSendTimeoutAfterInit { get; set; } = Timeout.Infinite;
    }

    abstract class FastMiddleProtocolStackBase : FastProtocolBase
    {
        protected FastPipeEnd Lower { get; }

        CriticalSection LockObj = new CriticalSection();
        protected FastAttachHandle LowerAttach { get; private set; }

        public FastMiddleProtocolOptionsBase MiddleOptions { get; }

        public FastMiddleProtocolStackBase(AsyncCleanuperLady lady, FastPipeEnd lower, FastPipeEnd upper, FastMiddleProtocolOptionsBase options, CancellationToken cancel = default)
            : base(lady, upper, options, cancel)
        {
            try
            {
                MiddleOptions = options;
                Lower = lower;

                LowerAttach = Lower.Attach(this.Lady, FastPipeEndAttachDirection.B_UpperSide);

                Lower.ExceptionQueue.Encounter(Upper.ExceptionQueue);
                Lower.LayerInfo.Encounter(Upper.LayerInfo);

                Lower.AddOnDisconnected(() => Upper.Disconnect());
                Upper.AddOnDisconnected(() => Lower.Disconnect());
            }
            catch
            {
                Lady.DisposeAllSafe();
                throw;
            }
        }

        public override void Disconnect(Exception ex = null)
        {
            try
            {
                Lower.Disconnect(ex);
            }
            finally { base.Disconnect(ex); }
        }
    }

    class FastSslProtocolOptions : FastMiddleProtocolOptionsBase { }

    class FastSslProtocolStack : FastMiddleProtocolStackBase
    {
        public class LayerInfo : LayerInfoBase, ILayerInfoSsl
        {
            public bool IsServerMode { get; internal set; }
            public string SslProtocol { get; internal set; }
            public string CipherAlgorithm { get; internal set; }
            public int CipherStrength { get; internal set; }
            public string HashAlgorithm { get; internal set; }
            public int HashStrength { get; internal set; }
            public string KeyExchangeAlgorithm { get; internal set; }
            public int KeyExchangeStrength { get; internal set; }
            public X509Certificate LocalCertificate { get; internal set; }
            public X509Certificate RemoteCertificate { get; internal set; }
        }

        public FastSslProtocolStack(AsyncCleanuperLady lady, FastPipeEnd lower, FastPipeEnd upper, FastSslProtocolOptions options,
            CancellationToken cancel = default) : base(lady, lower, upper, options ?? new FastSslProtocolOptions(), cancel) { }

        public async Task<FastSock> SslStartClient(SslClientAuthenticationOptions sslClientAuthenticationOptions, CancellationToken cancellationToken = default)
        {
            try
            {
                var lowerStream = LowerAttach.GetStream(autoFlush: false);

                var ssl = new PalSslStream(lowerStream).AddToLady(this);

                await ssl.AuthenticateAsClientAsync(sslClientAuthenticationOptions, CancelWatcher.CancelToken);

                LowerAttach.SetLayerInfo(new LayerInfo()
                {
                    IsServerMode = false,
                    SslProtocol = ssl.SslProtocol.ToString(),
                    CipherAlgorithm = ssl.CipherAlgorithm.ToString(),
                    CipherStrength = ssl.CipherStrength,
                    HashAlgorithm = ssl.HashAlgorithm.ToString(),
                    HashStrength = ssl.HashStrength,
                    KeyExchangeAlgorithm = ssl.KeyExchangeAlgorithm.ToString(),
                    KeyExchangeStrength = ssl.KeyExchangeStrength,
                    LocalCertificate = ssl.LocalCertificate,
                    RemoteCertificate = ssl.RemoteCertificate,
                }, this);

                var upperStreamWrapper = new FastPipeEndStreamWrapper(this.Lady, UpperAttach.PipeEnd, ssl, CancelWatcher.CancelToken);

                return new FastSock(this.Lady, this);
            }
            catch
            {
                await Lady;
                throw;
            }
        }
    }

    enum IPVersion
    {
        IPv4 = 0,
        IPv6 = 1,
    }

    enum ListenStatus
    {
        Trying,
        Listening,
        Stopped,
    }

    abstract class FastTcpListenerBase : IAsyncCleanupable
    {
        public class Listener
        {
            public IPVersion IPVersion { get; }
            public IPAddress IPAddress { get; }
            public int Port { get; }

            public ListenStatus Status { get; internal set; }
            public Exception LastError { get; internal set; }

            internal Task _InternalTask { get; }

            internal CancellationTokenSource _InternalSelfCancelSource { get; }
            internal CancellationToken _InternalSelfCancelToken { get => _InternalSelfCancelSource.Token; }

            public FastTcpListenerBase TcpListener { get; }

            public const long RetryIntervalStandard = 1 * 512;
            public const long RetryIntervalMax = 60 * 1000;

            internal Listener(FastTcpListenerBase listener, IPVersion ver, IPAddress addr, int port)
            {
                TcpListener = listener;
                IPVersion = ver;
                IPAddress = addr;
                Port = port;
                LastError = null;
                Status = ListenStatus.Trying;
                _InternalSelfCancelSource = new CancellationTokenSource();

                _InternalTask = ListenLoop();
            }

            static internal string MakeHashKey(IPVersion ipVer, IPAddress ipAddress, int port)
            {
                return $"{port} / {ipAddress} / {ipAddress.AddressFamily} / {ipVer}";
            }

            async Task ListenLoop()
            {
                AsyncAutoResetEvent networkChangedEvent = new AsyncAutoResetEvent();
                int eventRegisterId = BackgroundState<PalHostNetInfo>.EventListener.RegisterAsyncEvent(networkChangedEvent);

                Status = ListenStatus.Trying;

                int numRetry = 0;
                int lastNetworkInfoVer = BackgroundState<PalHostNetInfo>.Current.Version;

                try
                {
                    while (_InternalSelfCancelToken.IsCancellationRequested == false)
                    {
                        Status = ListenStatus.Trying;
                        _InternalSelfCancelToken.ThrowIfCancellationRequested();

                        int sleepDelay = (int)Math.Min(RetryIntervalStandard * numRetry, RetryIntervalMax);
                        if (sleepDelay >= 1)
                            sleepDelay = WebSocketHelper.RandSInt31() % sleepDelay;
                        await WebSocketHelper.WaitObjectsAsync(timeout: sleepDelay,
                            cancels: new CancellationToken[] { _InternalSelfCancelToken },
                            events: new AsyncAutoResetEvent[] { networkChangedEvent });
                        numRetry++;

                        int networkInfoVer = BackgroundState<PalHostNetInfo>.Current.Version;
                        if (lastNetworkInfoVer != networkInfoVer)
                        {
                            lastNetworkInfoVer = networkInfoVer;
                            numRetry = 0;
                        }

                        _InternalSelfCancelToken.ThrowIfCancellationRequested();

                        AsyncCleanuperLady listenLady = new AsyncCleanuperLady();

                        try
                        {
                            var listenTcp = TcpListener._InternalGetNewTcpStubForListen(listenLady, _InternalSelfCancelToken);

                            listenTcp.Listen(new IPEndPoint(IPAddress, Port));

                            Status = ListenStatus.Listening;

                            while (true)
                            {
                                _InternalSelfCancelToken.ThrowIfCancellationRequested();

                                AsyncCleanuperLady ladyForNewTcpStub = new AsyncCleanuperLady();

                                FastSock sock = await listenTcp.AcceptAsync(ladyForNewTcpStub);

                                TcpListener.InternalSocketAccepted(this, sock, ladyForNewTcpStub);
                            }
                        }
                        catch (Exception ex)
                        {
                            LastError = ex;
                        }
                        finally
                        {
                            await listenLady;
                        }
                    }
                }
                finally
                {
                    BackgroundState<PalHostNetInfo>.EventListener.UnregisterAsyncEvent(eventRegisterId);
                    Status = ListenStatus.Stopped;
                }
            }

            internal async Task _InternalStopAsync()
            {
                await _InternalSelfCancelSource.TryCancelAsync();
                try
                {
                    await _InternalTask;
                }
                catch { }
            }
        }

        readonly CriticalSection LockObj = new CriticalSection();

        readonly Dictionary<string, Listener> List = new Dictionary<string, Listener>();

        readonly Dictionary<Task, FastSock> RunningAcceptedTasks = new Dictionary<Task, FastSock>();

        readonly CancellationTokenSource CancelSource = new CancellationTokenSource();

        public delegate Task AcceptedProcCallback(Listener listener, FastSock newSock);

        AcceptedProcCallback AcceptedProc { get; }

        public int CurrentConnections
        {
            get
            {
                lock (RunningAcceptedTasks)
                    return RunningAcceptedTasks.Count;
            }
        }

        public FastTcpListenerBase(AsyncCleanuperLady lady, AcceptedProcCallback acceptedProc)
        {
            AcceptedProc = acceptedProc;

            AsyncCleanuper = new AsyncCleanuper(this);

            lady.Add(this);
        }

        internal protected abstract FastTcpProtocolStubBase _InternalGetNewTcpStubForListen(AsyncCleanuperLady lady, CancellationToken cancel);

        public Listener Add(int port, IPVersion? ipVer = null, IPAddress addr = null)
        {
            if (addr == null)
                addr = ((ipVer ?? IPVersion.IPv4) == IPVersion.IPv4) ? IPAddress.Any : IPAddress.IPv6Any;
            if (ipVer == null)
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork)
                    ipVer = IPVersion.IPv4;
                else if (addr.AddressFamily == AddressFamily.InterNetworkV6)
                    ipVer = IPVersion.IPv6;
                else
                    throw new ArgumentException("Unsupported AddressFamily.");
            }
            if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException("Port number is out of range.");

            lock (LockObj)
            {
                if (DisposeFlag.IsSet) throw new ObjectDisposedException("TcpListenManager");

                var s = Search(Listener.MakeHashKey((IPVersion)ipVer, addr, port));
                if (s != null)
                    return s;
                s = new Listener(this, (IPVersion)ipVer, addr, port);
                List.Add(Listener.MakeHashKey((IPVersion)ipVer, addr, port), s);
                return s;
            }
        }

        public async Task<bool> DeleteAsync(Listener listener)
        {
            Listener s;
            lock (LockObj)
            {
                string hashKey = Listener.MakeHashKey(listener.IPVersion, listener.IPAddress, listener.Port);
                s = Search(hashKey);
                if (s == null)
                    return false;
                List.Remove(hashKey);
            }
            await s._InternalStopAsync();
            return true;
        }

        Listener Search(string hashKey)
        {
            if (List.TryGetValue(hashKey, out Listener ret) == false)
                return null;
            return ret;
        }

        async Task InternalSocketAcceptedAsync(Listener listener, FastSock sock, AsyncCleanuperLady lady)
        {
            try
            {
                await AcceptedProc(listener, sock);
            }
            finally
            {
                await lady;
            }
        }

        void InternalSocketAccepted(Listener listener, FastSock sock, AsyncCleanuperLady lady)
        {
            try
            {
                Task t = InternalSocketAcceptedAsync(listener, sock, lady);

                if (t.IsCompleted)
                {
                    lady.DisposeAllSafe();
                }
                else
                {
                    lock (LockObj)
                        RunningAcceptedTasks.Add(t, sock);
                    t.ContinueWith(x =>
                    {
                        sock.DisposeSafe();
                        lock (LockObj)
                            RunningAcceptedTasks.Remove(t);
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("AcceptedProc error: " + ex.ToString());
            }
        }

        public Listener[] Listeners
        {
            get
            {
                lock (LockObj)
                    return List.Values.ToArray();
            }
        }

        Once DisposeFlag;
        public void Dispose()
        {
            if (DisposeFlag.IsFirstCall())
            {
            }
        }

        public async Task _CleanupAsyncInternal()
        {
            List<Listener> o = new List<Listener>();
            lock (LockObj)
            {
                List.Values.ToList().ForEach(x => o.Add(x));
                List.Clear();
            }

            foreach (Listener s in o)
                await s._InternalStopAsync().TryWaitAsync();

            List<Task> waitTasks = new List<Task>();
            List<FastSock> disconnectStubs = new List<FastSock>();

            lock (LockObj)
            {
                foreach (var v in RunningAcceptedTasks)
                {
                    disconnectStubs.Add(v.Value);
                    waitTasks.Add(v.Key);
                }
                RunningAcceptedTasks.Clear();
            }

            foreach (var sock in disconnectStubs)
            {
                try
                {
                    await sock.AsyncCleanuper;
                }
                catch { }
            }

            foreach (var task in waitTasks)
                await task.TryWaitAsync();

            Debug.Assert(CurrentConnections == 0);
        }

        public AsyncCleanuper AsyncCleanuper { get; }
    }

    class FastPalTcpListener : FastTcpListenerBase
    {
        public FastPalTcpListener(AsyncCleanuperLady lady, AcceptedProcCallback acceptedProc) : base(lady, acceptedProc) { }

        protected internal override FastTcpProtocolStubBase _InternalGetNewTcpStubForListen(AsyncCleanuperLady lady, CancellationToken cancel)
            => new FastPalTcpProtocolStub(lady, null, null, cancel);
    }
}
