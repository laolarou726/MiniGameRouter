using System.Text;
using MiniGameRouter.Helper;

namespace MiniGameRouter.Models.Router;

public class ConsistentHash<T> : IDisposable
{
    private int[] _ayKeys = null!; //缓存节点hash
    private int _replicate = 100; //虚拟节点数 count
    private readonly object _locker = new ();
    private SortedDictionary<int, T> Circle { get; } = new(); //虚拟的圆环，对2的32方取模

    public void Dispose()
    {
        Circle.Clear();
        _ayKeys = null!;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     初始化可迭代的节点数
    /// </summary>
    /// <param name="nodes">节点</param>
    public void Init(IEnumerable<T> nodes)
    {
        Init(nodes, _replicate);
    }

    /// <summary>
    ///     初始化可迭代的节点数，默认不缓存
    /// </summary>
    /// <param name="nodes">节点</param>
    /// <param name="replicate"></param>
    public void Init(IEnumerable<T> nodes, int replicate)
    {
        _replicate = replicate;

        foreach (var node in nodes) Add(node, false);

        _ayKeys = Circle.Keys.ToArray();
    }

    /// <summary>
    ///     添加节点，缓存
    /// </summary>
    /// <param name="node"></param>
    public void Add(T node)
    {
        Add(node, true);
    }

    /// <summary>
    ///     添加虚拟的圆环的节点
    /// </summary>
    /// <param name="node">节点</param>
    /// <param name="updateKeyArray">是否缓存node的hash</param>
    private void Add(T node, bool updateKeyArray)
    {
        if (node == null) return;

        lock (_locker)
        {
            for (var i = 0; i < _replicate; i++)
            {
                var hash = BetterHash(node.ToString() + i);
                Circle[hash] = node;
            }

            if (updateKeyArray) _ayKeys = Circle.Keys.ToArray();
        }
    }

    /// <summary>
    ///     删除真实机器节点，更新缓存
    /// </summary>
    /// <param name="node"></param>
    public void Remove(T node)
    {
        if (node == null) return;

        lock (_locker)
        {
            for (var i = 0; i < _replicate; i++)
            {
                var hash = BetterHash(node.ToString() + i);
                if (!Circle.Remove(hash)) throw new Exception("can not remove a node that not added");
            }

            _ayKeys = Circle.Keys.ToArray();
        }
    }

    private static int GetFirst(int[] ay, int val)
    {
        var begin = 0;
        var end = ay.Length - 1;

        if (ay[end] < val || ay[0] > val) return 0;

        while (end - begin > 1)
        {
            var mid = (end + begin) / 2;
            if (ay[mid] >= val)
                end = mid;
            else
                begin = mid;
        }

        if (ay[begin] > val || ay[end] < val)
            throw new Exception("should not happen");

        return end;
    }

    public T GetNode(string key)
    {
        lock (_locker)
        {
            var hash = BetterHash(key);
            var first = GetFirst(_ayKeys, hash);

            return Circle[_ayKeys[first]];
        }
    }

    /// <summary>
    ///     MurMurHash2算法,性能高,碰撞率低
    /// </summary>
    /// <param name="key">计算hash的字符串</param>
    /// <returns>hash值</returns>
    public static int BetterHash(string key)
    {
        var hash = Murmur2Helper.Hash(Encoding.ASCII.GetBytes(key));
        return (int)hash;
    }
}