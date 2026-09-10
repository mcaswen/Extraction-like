using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AgentReproduction.World
{
    public sealed class TestWorldBuilder : IDisposable
    {
        private readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();
        public T Own<T>(T value) where T : UnityEngine.Object { _owned.Add(value); return value; }
        public GameObject Root(string name, bool active = true)
        {
            GameObject value = Own(new GameObject(name));
            value.SetActive(active);
            return value;
        }
        public GameObject Cube(string name, Vector3 position, Vector3 size)
        {
            GameObject value = Own(GameObject.CreatePrimitive(PrimitiveType.Cube));
            value.name=name; value.transform.position=position; value.transform.localScale=size;
            return value;
        }
        public void Dispose()
        {
            for (int i=_owned.Count-1; i>=0; i--)
                if (_owned[i] != null && !AssetDatabase.Contains(_owned[i])) UnityEngine.Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
        }
    }
}
