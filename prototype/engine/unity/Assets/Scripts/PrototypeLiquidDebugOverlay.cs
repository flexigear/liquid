using System;
using System.Reflection;
using UnityEngine;

namespace Liquid
{
    public class PrototypeLiquidDebugOverlay : MonoBehaviour
    {
        [SerializeField] private bool visible = true;

        private Type liquidType;
        private Component liquidInstance;
        private GUIStyle labelStyle;
        private GUIStyle backgroundStyle;

        private void Awake()
        {
            liquidType = ResolveLiquidType();
        }

        private void Update()
        {
            if (!visible)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.BackQuote))
            {
                visible = false;
            }

            if (liquidInstance == null)
            {
                liquidInstance = FindLiquidInstance();
            }
        }

        private void OnGUI()
        {
            if (!visible)
            {
                return;
            }

            EnsureStyle();

            float x = 18f;
            float y = 18f;
            float width = 320f;
            float lineHeight = 24f;

            DrawLine(new Rect(x, y, width, lineHeight), "Liquid Debug", true);
            y += lineHeight;

            if (liquidType == null)
            {
                DrawLine(new Rect(x, y, width, lineHeight), "ZibraLiquid type not found", false);
                return;
            }

            if (liquidInstance == null)
            {
                liquidInstance = FindLiquidInstance();
            }

            if (liquidInstance == null)
            {
                DrawLine(new Rect(x, y, width, lineHeight), "ZibraLiquidVolume not found", false);
                return;
            }

            DrawLine(new Rect(x, y, width, lineHeight), "Initialized: " + ReadValue("Initialized"), false);
            y += lineHeight;
            DrawLine(new Rect(x, y, width, lineHeight), "Simulation Frame: " + ReadValue("SimulationInternalFrame"), false);
            y += lineHeight;
            DrawLine(new Rect(x, y, width, lineHeight), "Particle Count: " + ReadValue("CurrentParticleNumber"), false);
            y += lineHeight;
            DrawLine(new Rect(x, y, width, lineHeight), "Run Simulation: " + ReadValue("RunSimulation"), false);
            y += lineHeight;
            DrawLine(new Rect(x, y, width, lineHeight), "Run Rendering: " + ReadValue("RunRendering"), false);
            y += lineHeight;
            DrawLine(new Rect(x, y, width, lineHeight), "Press ` to hide this panel", false);
        }

        private void EnsureStyle()
        {
            if (labelStyle != null)
            {
                return;
            }

            backgroundStyle = new GUIStyle(GUI.skin.box);
            labelStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 12,
                richText = false,
            };
            labelStyle.padding = new RectOffset(10, 10, 4, 4);
            labelStyle.normal.textColor = Color.white;
            labelStyle.hover.textColor = Color.white;
            labelStyle.active.textColor = Color.white;
            labelStyle.focused.textColor = Color.white;
        }

        private void DrawLine(Rect rect, string text, bool isHeader)
        {
            Color previousBackgroundColor = GUI.backgroundColor;
            Color previousContentColor = GUI.contentColor;

            GUI.backgroundColor = isHeader ? new Color(0.1f, 0.18f, 0.28f, 0.92f) : new Color(0.02f, 0.02f, 0.02f, 0.78f);
            GUI.contentColor = Color.white;

            GUI.Box(rect, GUIContent.none, backgroundStyle);
            GUI.Label(rect, text, labelStyle);

            GUI.backgroundColor = previousBackgroundColor;
            GUI.contentColor = previousContentColor;
        }

        private string ReadValue(string memberName)
        {
            if (liquidInstance == null)
            {
                return "n/a";
            }

            PropertyInfo property = liquidType.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
            if (property != null)
            {
                object value = property.GetValue(liquidInstance);
                return value != null ? value.ToString() : "null";
            }

            FieldInfo field = liquidType.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                object value = field.GetValue(liquidInstance);
                return value != null ? value.ToString() : "null";
            }

            return "missing";
        }

        private Component FindLiquidInstance()
        {
            if (liquidType == null)
            {
                return null;
            }

            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour != null && liquidType.IsInstanceOfType(behaviour))
                {
                    return behaviour;
                }
            }

            return null;
        }

        private static Type ResolveLiquidType()
        {
            return Type.GetType("com.zibra.liquid.Solver.ZibraLiquid, ZibraAI.ZibraEffects.Liquid");
        }
    }
}
