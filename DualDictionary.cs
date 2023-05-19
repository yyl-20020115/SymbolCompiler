using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace SymbolCompiler;

public class DualDictionary<TKey,TValue> : 
    ICollection<KeyValuePair<TKey, TValue>>, 
    IEnumerable<KeyValuePair<TKey, TValue>>, 
    IEnumerable, IDictionary<TKey, TValue>, 
    IReadOnlyCollection<KeyValuePair<TKey, TValue>>, 
    IReadOnlyDictionary<TKey, TValue>, 
    ICollection, 
    IDictionary
    where TKey : notnull where TValue : notnull {
    public IDictionary<TKey, TValue> MainData { get; } = new Dictionary<TKey, TValue>();
    public IDictionary<TValue, TKey> AuxData { get; } = new Dictionary<TValue, TKey>();
    public TValue this[TKey key]
    {
        get => this.MainData[key];
        set {
            if(!this.MainData.TryGetValue(key,out var value1))
            {
                this.MainData.Add(key, value);
                this.AuxData.Add(value, key);
            }
            else
            {
                this.MainData[key] = value;
                this.AuxData[value] = key;
            }
        }
    }
    public TKey this[TValue key]
    {
        get => this.AuxData[key];
        set
        {
            if (!this.AuxData.TryGetValue(key, out var value1))
            {
                this.AuxData.Add(key, value);
                this.MainData.Add(value, key);
            }
            else
            {
                this.AuxData[key] = value;
                this.MainData[value] = key;
            }
        }
    }
    public object? this[object key] 
    {
        get => ((IDictionary)MainData)[key] ?? ((IDictionary)AuxData)[key];
        set {
            if (key is TKey)
            {
                ((IDictionary)MainData)[key] = value;
                if (value != null)
                {
                    ((IDictionary)AuxData)[value] = key;
                }
            }else if(key is TValue)
            {
                ((IDictionary)AuxData)[key] = value;
                if (value != null)
                {
                    ((IDictionary)MainData)[value] = key;
                }
            }

        }
    }
    public ICollection<TKey> Keys => MainData.Keys;
    public ICollection<TValue> Values => MainData.Values;
    public int Count => MainData.Count;
    public bool IsReadOnly => MainData.IsReadOnly;
    public bool IsSynchronized => ((ICollection)MainData).IsSynchronized;
    public object SyncRoot => ((ICollection)MainData).SyncRoot;
    public bool IsFixedSize => ((IDictionary)MainData).IsFixedSize;
    IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => MainData.Keys;
    ICollection IDictionary.Keys => ((IDictionary)MainData).Keys;
    IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => MainData.Values;
    ICollection IDictionary.Values => ((IDictionary)MainData).Values;
    public void Add(TKey key, TValue value)
    {
        this.MainData.Add(key, value);
        this.AuxData.Add(value, key);
    }
    /// <summary>
    /// reurn true if add operation performed
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public bool AddOrUpdate(TKey key, TValue value)
    {
        if (this.MainData.ContainsKey(key))
        {
            this.MainData[key] = value;
            this.AuxData[value] = key;
            return false;
        }
        else
        {
            this.Add(key, value);
            return true;
        }
    }
    public bool AddOrUpdate(TValue key, TKey value)
    {
        if (this.AuxData.ContainsKey(key))
        {
            this.AuxData[key] = value;
            this.MainData[value] = key;
            return false;
        }
        else
        {
            ((DualDictionary<TKey,TValue>)this).Add(key, value);
            return true;
        }
    }
    public void Add(KeyValuePair<TKey, TValue> item)
    {
        this.MainData.Add(item);
        this.AuxData.Add(new KeyValuePair<TValue, TKey>(item.Value, item.Key));
    }
    public void Add(object key, object? value)
    {
        if(key is TKey tk && value is TValue tv)
        {
            this.MainData.Add(tk, tv);
            this.AuxData.Add(tv, tk);
        }
        else if(key is TValue tv1 && value is TKey tk1)
        {
            this.MainData.Add(tk1, tv1);
            this.AuxData.Add(tv1, tk1);
        }
    }
    public void Clear()
    {
        this.MainData.Clear();
        this.AuxData.Clear();
    }
    public bool Contains(KeyValuePair<TKey, TValue> item) => MainData.Contains(item);
    public bool Contains(KeyValuePair<TValue, TKey> item) => AuxData.Contains(item);
    public bool Contains(object key) => ((IDictionary)MainData).Contains(key) || ((IDictionary)AuxData).Contains(key);
    public bool ContainsKey(TKey key) => MainData.ContainsKey(key);
    public bool ContainsKey(TValue key) => AuxData.ContainsKey(key);
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex) => MainData.CopyTo(array, arrayIndex);
    public void CopyTo(KeyValuePair<TValue, TKey>[] array, int arrayIndex) => AuxData.CopyTo(array, arrayIndex);
    public void CopyTo(Array array, int index) => ((ICollection)MainData).CopyTo(array, index);
    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => MainData.GetEnumerator();
    public bool Remove(TKey key)
    {
        if(this.MainData.TryGetValue(key,out var value1))
        {
            AuxData.Remove(value1);
            return MainData.Remove(key);
        }
        return false;
    }
    public bool Remove(TValue key)
    {
        if (this.AuxData.TryGetValue(key, out var value1))
        {
            MainData.Remove(value1);
            return AuxData.Remove(key);
        }
        return false;
    }
    public bool Remove(KeyValuePair<TKey, TValue> item) => MainData.Remove(item)
            && AuxData.Remove(new KeyValuePair<TValue, TKey>(item.Value, item.Key));
    public void Remove(object key)
    {
        if(key is TKey && MainData.TryGetValue((TKey)key, out var value1))
        {
            ((IDictionary)MainData).Remove(key);
            ((IDictionary)AuxData).Remove(value1);
        }
        if (key is TValue && AuxData.TryGetValue((TValue)key, out var value2))
        {
            ((IDictionary)AuxData).Remove(key);
            ((IDictionary)MainData).Remove(value2);
        }
    }
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value) => MainData.TryGetValue(key, out value);
    public bool TryGetValue(TValue key, [MaybeNullWhen(false)] out TKey value) => AuxData.TryGetValue(key, out value);
    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)MainData).GetEnumerator();
    IDictionaryEnumerator IDictionary.GetEnumerator() => ((IDictionary)MainData).GetEnumerator();
}
