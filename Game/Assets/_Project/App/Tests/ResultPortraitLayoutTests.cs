using System;
using System.Reflection;
using HumanLastRampart.Core.Definitions;
using NUnit.Framework;
using UnityEngine;

namespace HumanLastRampart.App.Tests
{
    public sealed class ResultPortraitLayoutTests
    {
        [TestCase(288, 384, false, 288f, 360f, 1, 1)]
        [TestCase(1152, 1536, false, 288f, 360f, 4, 4)]
        [TestCase(1024, 1536, false, 256f, 352f, 4, 4)]
        [TestCase(288, 384, true, 288f, 360f, 1, 1)]
        public void Portrait_KeepsCellAspectAndCommonFootLine(int width, int height, bool idle,
            float cellWidth, float foot, int columns, int rows)
        {
            var parent = new GameObject("Portrait Test", typeof(RectTransform));
            var def = ScriptableObject.CreateInstance<UnitDef>();
            var texture = new Texture2D(width, height);
            Texture2D fallback = idle ? new Texture2D(1024, 1536) : null;
            try
            {
                const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(UnitDef).GetField("sheet", fields).SetValue(def, fallback != null ? fallback : texture);
                if (idle) typeof(UnitDef).GetField("idleSheet", fields).SetValue(def, texture);
                Type board = Type.GetType("HumanLastRampart.Meta.ResultBoard, HLR.Meta", true);
                MethodInfo draw = board.GetMethod("Portrait", BindingFlags.Static | BindingFlags.NonPublic);
                var at = new Vector2(42f, 80f);
                draw.Invoke(null, new object[] { parent.GetComponent<RectTransform>(), def, at, 192f, null });
                var rect = (RectTransform)parent.transform.GetChild(0);
                Assert.That(rect.sizeDelta.x, Is.EqualTo(192f * cellWidth / 384f).Within(0.001f));
                Assert.That(rect.sizeDelta.y, Is.EqualTo(192f));
                Assert.That(rect.pivot.y, Is.EqualTo((384f - foot) / 384f).Within(0.0001f));
                Assert.That(rect.anchoredPosition.x, Is.EqualTo(at.x));
                Assert.That(rect.anchoredPosition.y, Is.EqualTo(at.y - 192f * (0.5f - 32f / 384f)).Within(0.001f));
                // Reflection keeps this test assembly from acquiring a new Meta/UI module dependency.
                Component image = rect.GetComponent("RawImage");
                Type imageType = image.GetType();
                Assert.That(imageType.GetProperty("texture").GetValue(image), Is.SameAs(texture));
                var uv = (Rect)imageType.GetProperty("uvRect").GetValue(image);
                Assert.That(uv.x, Is.EqualTo(0f));
                Assert.That(uv.y, Is.EqualTo(1f - 1f / rows).Within(0.0001f));
                Assert.That(uv.width, Is.EqualTo(1f / columns).Within(0.0001f));
                Assert.That(uv.height, Is.EqualTo(1f / rows).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
                UnityEngine.Object.DestroyImmediate(def);
                UnityEngine.Object.DestroyImmediate(texture);
                if (fallback != null) UnityEngine.Object.DestroyImmediate(fallback);
            }
        }
    }
}
