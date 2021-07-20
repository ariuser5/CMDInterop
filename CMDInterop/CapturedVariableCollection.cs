using System;
using System.Collections;
using System.Collections.Generic;

namespace CMDInterop
{
    public class CapturedVariableCollection : ICollection<string>
    {
        readonly Dictionary<string, string> _map;

        internal CapturedVariableCollection()
        {
            this._map = new Dictionary<string, string>();
        }

        internal CapturedVariableCollection(IEnumerable<string> source)
            : this()
        {
            foreach(var key in source) {
                this._map.Add(key, null);
            }
        }


        public string this[string varName] {
            get => this._map[varName];
            set {
                if(!this._map.ContainsKey(varName)) {
                    this.Add(varName);
                }

                this._map[varName] = value;
            }
        }

        public int Count => this._map.Count;

        bool ICollection<string>.IsReadOnly => false;




        public void Add(string item)
        {
            if(this.Contains(item) == false) {
                this._map.Add(item, null);
            }
        }

        public void Clear()
        {
            this._map.Clear(); ;
        }

        public bool Contains(string item)
        {
            return this._map.ContainsKey(item); ;
        }

        void ICollection<string>.CopyTo(string[] array, int arrayIndex)
        {
            throw new NotSupportedException();
        }

        public IEnumerator<string> GetEnumerator()
        {
            return this._map.Keys.GetEnumerator();
        }

        public bool Remove(string item)
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }


        //public static implicit operator CapturedVariableCollection(string[] source)
        //{
        //    return new CapturedVariableCollection(source);
        //}

    }
}
