using System.Collections.Generic;
using System.Reflection;
using HumanLastRampart.Core.Definitions;
using UnityEngine;

namespace HumanLastRampart.Core.Tests
{
    // 시험용 정의 찾기: 넣어 준 정의만 돌려준다
    sealed class FakeDefinitions : IDefinitionLookup
    {
        readonly List<DefinitionBase> items = new List<DefinitionBase>();

        public FakeDefinitions Add(DefinitionBase item)
        {
            items.Add(item);
            return this;
        }

        public T Get<T>(string id) where T : DefinitionBase
        {
            foreach (DefinitionBase item in items)
            {
                if (item is T typed && item.Id == id)
                {
                    return typed;
                }
            }

            return null;
        }

        public IReadOnlyList<T> All<T>() where T : DefinitionBase
        {
            var list = new List<T>();
            foreach (DefinitionBase item in items)
            {
                if (item is T typed)
                {
                    list.Add(typed);
                }
            }

            return list;
        }
    }

    static class TestDefs
    {
        // 정의의 비공개 직렬화 필드를 시험에서만 바꾼다
        public static T Make<T>(string id, params (string field, object value)[] fields) where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            Set(instance, typeof(DefinitionBase), "id", id);
            foreach ((string field, object value) in fields)
            {
                Set(instance, typeof(T), field, value);
            }

            return instance;
        }

        static void Set(object target, System.Type type, string field, object value)
        {
            FieldInfo info = type.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (info == null)
            {
                throw new System.ArgumentException($"{type.Name}.{field} 필드가 없습니다");
            }

            info.SetValue(target, value);
        }
    }
}