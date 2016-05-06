using System.Collections;
#if NETFX_CORE
  using Windows.System.Threading;
#else
    using System.Threading;
#endif

/// <summary>
/// Represents a threaded worker that can interface nicely with Unity Behaviors
/// </summary>
/// <see cref="http://answers.unity3d.com/questions/357033/unity3d-and-c-coroutines-vs-threading.html"/>
/// <remarks>
/// Derive this class to create custom threaded jobs.  Begin processing asynchronously by calling Start() and use a Coroutine thusly
/// to rejoin to the main thread:
///     yield return StartCoroutine(myJob.WaitFor());
/// </remarks>
public class ThreadedJob
{
    private bool _isDone;
    private readonly object _handle = new object();
    //private System.Threading.Thread _thread;

    public bool IsDone
    {
        get
        {
            bool tmp;
            lock (_handle)
            {
                tmp = _isDone;
            }
            return tmp;
        }
        set
        {
            lock (_handle)
            {
                _isDone = value;
            }
        }
    }

    public virtual void Start()
    {
        //ThreadPool.QueueUserWorkItem(_ => Run());

#if !NETFX_CORE
        ThreadPool.QueueUserWorkItem(_ => Run());
#else
  var asyncAction = Windows.System.Threading.ThreadPool.RunAsync((workItem) =>
  {
  Run();
  });
  //m_workItem = asyncAction;
#endif
    }

    public virtual void Abort()
    {
        //_thread.Abort();
    }

    protected virtual void ThreadFunction() { }

    protected virtual void OnFinished() { }

    public virtual bool Update()
    {
        if (IsDone)
        {
            OnFinished();
            return true;
        }
        return false;
    }
    public IEnumerator WaitFor()
    {
        while (!Update())
        {
            yield return null;
        }
    }
    private void Run()
    {
        ThreadFunction();
        IsDone = true;
    }
}
