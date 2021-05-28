using System;
using System.Collections;
using System.Collections.Generic;

public class KeySortedList<T, D> where D : IComparable<D>
{
    private List<T> values;
    private List<D> keys;

    public int Count
    {
        get
        {
            return values.Count;
        }
    }

    public T this[int index]
    {
        get
        {
            return values[index];
        }
    }

    public KeySortedList()
    {
        values = new List<T>();
        keys = new List<D>();
    }

    public D KeyAt(int index)
    {
        return keys[index];
    }

    public D KeyOf(T value)
    {
        return keys[values.IndexOf(value)];
    }

    public void Add(T value, D key)
    {
        if(keys.Contains(key))
        {
            int i = keys.IndexOf(key);
            keys.RemoveAt(i);
            values.RemoveAt(i);
        }
        keys.InsertIntoSortedList(key);
        values.Insert(keys.IndexOf(key), value);
    }

    public void RemoveAt(int i)
    {
        if(i >= 0 && i < Count)
        {
            values.RemoveAt(i);
            keys.RemoveAt(i);
        }
    }

    public void Remove(T value)
    {
        while(values.Contains(value))
        {
            int i = values.IndexOf(value);
            keys.RemoveAt(i);
            values.RemoveAt(i);
        }
    }

    public bool Contains(T value)
    {
        return values.Contains(value);
    }
}
