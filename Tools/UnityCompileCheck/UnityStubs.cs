// Stub TỐI THIỂU của các API Unity mà code PawFit đang dùng — CHỈ để compile thử bằng dotnet trong
// container (không có Unity Editor). Chữ ký hàm chép theo Unity 6; không có logic thật.
// Khi code Unity dùng thêm API mới mà stub chưa có => compile check báo lỗi => bổ sung stub ở đây.
#pragma warning disable CS0067, CS0649, CS1591, CS8618
using System;
using System.Collections.Generic;

namespace UnityEngine
{
    public class Object
    {
        public string name { get; set; }
        public static void Destroy(Object obj) { }
        public static void DestroyImmediate(Object obj) { }
        public static void DontDestroyOnLoad(Object target) { }
        public static T FindAnyObjectByType<T>() where T : Object => null;
        public static T[] FindObjectsByType<T>(FindObjectsSortMode sortMode) where T : Object => new T[0];
        public static T Instantiate<T>(T original, Transform parent) where T : Object => original;
        public static implicit operator bool(Object o) => !ReferenceEquals(o, null);
    }

    public enum FindObjectsSortMode { None, InstanceID }

    public class Component : Object
    {
        public Transform transform => null;
        public GameObject gameObject => null;
        public T GetComponent<T>() => default;
        public T GetComponentInParent<T>() => default;
        public T GetComponentInChildren<T>() => default;
    }

    public class Behaviour : Component { public bool enabled { get; set; } }
    public class Texture : Object { }
    public class Texture2D : Texture { }
    public sealed class CanvasGroup : Behaviour { public float alpha { get; set; } public bool blocksRaycasts { get; set; } public bool interactable { get; set; } }

    public class MonoBehaviour : Behaviour
    {
        public Coroutine StartCoroutine(System.Collections.IEnumerator routine) => null;
    }

    public sealed class Coroutine { }

    public class YieldInstruction { }

    public sealed class WaitForSeconds : YieldInstruction { public WaitForSeconds(float seconds) { } }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new() => new T();
    }

    public sealed class GameObject : Object
    {
        public GameObject() { }
        public GameObject(string name) { }
        public GameObject(string name, params Type[] components) { }
        public bool activeSelf => true;
        public void SetActive(bool value) { }
        public Transform transform => null;
        public string tag { get; set; }
        public T AddComponent<T>() where T : Component => default;
        public T GetComponent<T>() => default;
        public static GameObject CreatePrimitive(PrimitiveType type) => new GameObject();
    }

    public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad }

    public class Transform : Component
    {
        public Transform parent { get; set; }
        public int childCount => 0;
        public Transform GetChild(int index) => null;
        public Vector3 right => default;
        public Vector3 up => default;
        public Vector3 forward => default;
        public Vector3 lossyScale => default;
        public Vector3 InverseTransformPoint(Vector3 position) => position;
        public Vector3 InverseTransformDirection(Vector3 direction) => direction;
        public Vector3 InverseTransformVector(Vector3 vector) => vector;
        public Vector3 TransformDirection(Vector3 direction) => direction;
        public Vector3 TransformVector(Vector3 vector) => vector;
        public void Rotate(float xAngle, float yAngle, float zAngle) { }
        public Vector3 position { get; set; }
        public Vector3 localPosition { get; set; }
        public Quaternion rotation { get; set; }
        public Quaternion localRotation { get; set; }
        public Vector3 localScale { get; set; }
        public void SetParent(Transform parent, bool worldPositionStays) { }
        public void SetAsLastSibling() { }
        public Transform Find(string n) => null;
        public void SetPositionAndRotation(Vector3 position, Quaternion rotation) { }
        public Vector3 TransformPoint(Vector3 position) => position;
    }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public float magnitude => 0f;
        public static Vector2 zero => default;
        public static Vector2 one => new Vector2(1, 1);
        public static Vector2 operator -(Vector2 a, Vector2 b) => a;
        public static Vector2 operator +(Vector2 a, Vector2 b) => a;
        public static Vector2 operator *(Vector2 a, float d) => a;
        public static float Distance(Vector2 a, Vector2 b) => 0f;
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) => a;
        public static implicit operator Vector2(Vector3 v) => new Vector2(v.x, v.y);
        public static implicit operator Vector3(Vector2 v) => new Vector3(v.x, v.y, 0f);
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => default;
        public static Vector3 one => new Vector3(1, 1, 1);
        public static Vector3 up => new Vector3(0, 1, 0);
        public static Vector3 right => new Vector3(1, 0, 0);
        public static Vector3 forward => new Vector3(0, 0, 1);
        public Vector3 normalized => this;
        public float magnitude => 0f;
        public float sqrMagnitude => 0f;
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a;
        public static Vector3 Scale(Vector3 a, Vector3 b) => a;
        public static float Distance(Vector3 a, Vector3 b) => 0f;
        public static Vector3 operator +(Vector3 a, Vector3 b) => a;
        public static Vector3 operator -(Vector3 a, Vector3 b) => a;
        public static Vector3 operator *(Vector3 a, float d) => a;
        public static Vector3 operator *(float d, Vector3 a) => a;
    }

    public struct Quaternion
    {
        public static Quaternion identity => default;
        public static Quaternion Euler(float x, float y, float z) => default;
        public static Quaternion LookRotation(Vector3 forward) => default;
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => a;
        public static Quaternion AngleAxis(float angle, Vector3 axis) => default;
        public static Quaternion operator *(Quaternion a, Quaternion b) => a;
        public static Vector3 operator *(Quaternion rotation, Vector3 point) => point;
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new Color(1, 1, 1);
        public static Color magenta => new Color(1, 0, 1);
        public static Color Lerp(Color a, Color b, float t) => a;
        public static Color HSVToRGB(float h, float s, float v) => default;
        public static implicit operator Color32(Color c) => default;
        public static Color operator *(Color a, float b) => a;
    }

    public struct Color32 { public byte r, g, b, a; }

    public static class ColorUtility { public static bool TryParseHtmlString(string htmlString, out Color color) { color = default; return true; } }

    public static class Mathf
    {
        public const float PI = 3.14159265f;
        public const float Deg2Rad = PI / 180f;
        public const float Rad2Deg = 57.29578f;
        public static float Sign(float f) => 1f;
        public static float Atan2(float y, float x) => 0f;
        public static float Floor(float f) => 0f;
        public static float Min(float a, float b) => a;
        public static int Min(int a, int b) => a;
        public static float MoveTowards(float current, float target, float maxDelta) => target;
        public static int CeilToInt(float f) => 0;
        public static int Clamp(int v, int min, int max) => v;
        public static float Sin(float f) => 0f;
        public static float Cos(float f) => 0f;
        public static float Abs(float f) => 0f;
        public static float Exp(float f) => 0f;
        public static float Pow(float f, float p) => 0f;
        public static float Sqrt(float f) => 0f;
        public static float Max(float a, float b) => a;
        public static float Clamp(float v, float min, float max) => v;
        public static float Clamp01(float v) => v;
        public static float Lerp(float a, float b, float t) => a;
        public static int RoundToInt(float f) => 0;
    }

    public class Material : Object
    {
        public Material(Shader shader) { }
        public Material(Material source) { }
        public Color color { get; set; }
        public bool HasProperty(string name) => true;
        public void SetFloat(string name, float value) { }
        public void SetColor(string name, Color value) { }
    }

    public sealed class Shader : Object { public static Shader Find(string name) => null; public bool isSupported => true; }

    public class Renderer : Component
    {
        public Material sharedMaterial { get; set; }
        public Rendering.ShadowCastingMode shadowCastingMode { get; set; }
    }

    public sealed class Mesh : Object
    {
        public int blendShapeCount => 0;
        public int vertexCount => 0;
        public string GetBlendShapeName(int shapeIndex) => "";
    }

    public sealed class SkinnedMeshRenderer : Renderer
    {
        public Mesh sharedMesh { get; set; }
        public Bounds localBounds { get; set; }
        public void SetBlendShapeWeight(int index, float value) { }
    }

    public static class Resources
    {
        public static T Load<T>(string path) where T : Object => null;
        public static T GetBuiltinResource<T>(string path) where T : Object => null;
    }

    public sealed class Font : Object { public Material material => null; }

    public enum TextAnchor { UpperLeft, UpperCenter, UpperRight, MiddleLeft, MiddleCenter, MiddleRight, LowerLeft, LowerCenter, LowerRight }

    public enum TextAlignment { Left, Center, Right }

    public sealed class TextMesh : Component
    {
        public string text { get; set; }
        public Font font { get; set; }
        public int fontSize { get; set; }
        public float characterSize { get; set; }
        public TextAnchor anchor { get; set; }
        public TextAlignment alignment { get; set; }
        public FontStyle fontStyle { get; set; }
        public Color color { get; set; }
    }

    public class MeshRenderer : Renderer { }

    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }

    public sealed class Canvas : Behaviour
    {
        public RenderMode renderMode { get; set; }
        public int sortingOrder { get; set; }
        public float scaleFactor { get; set; }
    }

    public static class RectTransformUtility
    {
        public static bool RectangleContainsScreenPoint(RectTransform rect, Vector2 screenPoint, Camera cam) => false;
    }

    public class RectTransform : Transform
    {
        public Vector2 anchorMin { get; set; }
        public Vector2 anchorMax { get; set; }
        public Vector2 pivot { get; set; }
        public Vector2 anchoredPosition { get; set; }
        public Vector2 sizeDelta { get; set; }
        public Vector2 offsetMin { get; set; }
        public Vector2 offsetMax { get; set; }
    }

    public sealed class TextAsset : Object { public string text => ""; }

    public static class JsonUtility { public static T FromJson<T>(string json) => default; }

    public static class Random
    {
        public static float value => 0f;
        public static int Range(int minInclusive, int maxExclusive) => minInclusive;
        public static float Range(float minInclusive, float maxInclusive) => minInclusive;
    }

    public sealed class AudioClip : Object
    {
        public static AudioClip Create(string name, int lengthSamples, int channels, int frequency, bool stream) => new AudioClip();
        public bool SetData(float[] data, int offsetSamples) => true;
    }

    public sealed class AudioSource : Behaviour
    {
        public bool playOnAwake { get; set; }
        public float spatialBlend { get; set; }
        public float volume { get; set; }
        public float pitch { get; set; }
        public void PlayOneShot(AudioClip clip) { }
    }

    public sealed class AudioListener : Behaviour { public static float volume { get; set; } }

    public sealed class Animator : Behaviour { }

    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
        public bool Contains(Vector2 point) => false;
    }

    public struct Matrix4x4 { public static Matrix4x4 Scale(Vector3 vector) => default; }

    public enum FontStyle { Normal, Bold, Italic, BoldAndItalic }

    public sealed class GUIStyle
    {
        public GUIStyle() { }
        public GUIStyle(GUIStyle other) { }
        public int fontSize { get; set; }
        public bool wordWrap { get; set; }
        public FontStyle fontStyle { get; set; }
        public float fixedHeight { get; set; }
    }

    public sealed class GUISkin : Object { public GUIStyle label => null; public GUIStyle button => null; }

    public sealed class GUIContent { public static GUIContent none => null; }

    public sealed class GUILayoutOption { }

    public static class GUI
    {
        public static Matrix4x4 matrix { get; set; }
        public static GUISkin skin => null;
        public static bool Button(Rect position, string text, GUIStyle style) => false;
        public static void Box(Rect position, GUIContent content) { }
    }

    public static class GUILayout
    {
        public static void BeginArea(Rect screenRect) { }
        public static void EndArea() { }
        public static Vector2 BeginScrollView(Vector2 scrollPosition, params GUILayoutOption[] options) => scrollPosition;
        public static void EndScrollView() { }
        public static void BeginHorizontal(params GUILayoutOption[] options) { }
        public static void EndHorizontal() { }
        public static void Space(float pixels) { }
        public static void Label(string text, GUIStyle style, params GUILayoutOption[] options) { }
        public static bool Button(string text, GUIStyle style, params GUILayoutOption[] options) => false;
        public static GUILayoutOption Width(float width) => null;
    }

    public static class Debug
    {
        public static void Log(object message) { }
        public static void LogWarning(object message) { }
        public static void LogError(object message) { }
    }

    public static class Time
    {
        public static float deltaTime => 0f;
        public static float unscaledDeltaTime => 0f;
        public static float time => 0f;
        public static float unscaledTime => 0f;
        public static double unscaledTimeAsDouble => 0.0;
    }

    public enum TouchPhase { Began, Moved, Stationary, Ended, Canceled }

    public struct Touch { public Vector2 position { get; set; } public TouchPhase phase { get; set; } }

    public enum KeyCode { None, Alpha1, Alpha2, Alpha3, N, F1 }

    public static class Input
    {
        public static int touchCount => 0;
        public static bool simulateMouseWithTouches { get; set; }
        public static Vector3 mousePosition => default;
        public static Touch GetTouch(int index) => default;
        public static bool GetMouseButton(int button) => false;
        public static bool GetMouseButtonDown(int button) => false;
        public static bool GetMouseButtonUp(int button) => false;
        public static bool GetKeyDown(KeyCode key) => false;
    }

    public enum ScreenOrientation { Portrait, PortraitUpsideDown, LandscapeLeft, LandscapeRight, AutoRotation }

    public static class Screen
    {
        public static float dpi => 0f;
        public static int width => 1080;
        public static int height => 1920;
        public static ScreenOrientation orientation { get; set; }
    }

    public static class Application
    {
        public static string persistentDataPath => "";
        public static bool isMobilePlatform => false;
        public static int targetFrameRate { get; set; }
        public static string version => "";
    }

    public static class GUIUtility { public static string systemCopyBuffer { get; set; } }

    public enum CameraClearFlags { Skybox, SolidColor, Depth, Nothing }

    public sealed class Camera : Behaviour
    {
        public static Camera main => null;
        public float fieldOfView { get; set; }
        public float nearClipPlane { get; set; }
        public float farClipPlane { get; set; }
        public CameraClearFlags clearFlags { get; set; }
        public Color backgroundColor { get; set; }
        public Ray ScreenPointToRay(Vector3 pos) => default;
    }

    public struct Ray { public Vector3 GetPoint(float distance) => default; }

    public struct Plane
    {
        public Plane(Vector3 inNormal, Vector3 inPoint) { }
        public bool Raycast(Ray ray, out float enter) { enter = 0f; return false; }
    }

    public struct RaycastHit { public Collider collider => null; }

    public static class Physics { public static bool Raycast(Ray ray, out RaycastHit hitInfo, float maxDistance) { hitInfo = default; return false; } }

    public struct Bounds
    {
        public Bounds(Vector3 center, Vector3 size) { }
        public void Expand(float amount) { }
    }

    public class Collider : Component { }

    public sealed class BoxCollider : Collider { public Vector3 size { get; set; } public Vector3 center { get; set; } }

    public sealed class SphereCollider : Collider { public Vector3 center { get; set; } public float radius { get; set; } }

    public sealed class CapsuleCollider : Collider
    {
        public int direction { get; set; }
        public Vector3 center { get; set; }
        public float radius { get; set; }
        public float height { get; set; }
    }

    public enum LightType { Spot, Directional, Point }

    public enum LightShadows { None, Hard, Soft }

    public sealed class Light : Behaviour
    {
        public LightType type { get; set; }
        public Color color { get; set; }
        public float intensity { get; set; }
        public LightShadows shadows { get; set; }
        public float shadowStrength { get; set; }
    }

    public enum FogMode { Linear = 1, Exponential, ExponentialSquared }

    public static class RenderSettings
    {
        public static Rendering.AmbientMode ambientMode { get; set; }
        public static Color ambientSkyColor { get; set; }
        public static Color ambientEquatorColor { get; set; }
        public static Color ambientGroundColor { get; set; }
        public static bool fog { get; set; }
        public static FogMode fogMode { get; set; }
        public static Color fogColor { get; set; }
        public static float fogStartDistance { get; set; }
        public static float fogEndDistance { get; set; }
    }

    public static class QualitySettings
    {
        public static string[] names => new string[0];
        public static int GetQualityLevel() => 0;
        public static void SetQualityLevel(int index, bool applyExpensiveChanges) { }
        public static Rendering.RenderPipelineAsset renderPipeline { get; set; }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class RequireComponent : Attribute { public RequireComponent(Type t) { } }

    public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { } }
}

namespace UnityEngine.Events
{
    public delegate void UnityAction();

    public class UnityEvent { public void AddListener(UnityAction call) { } }
}

namespace UnityEngine.EventSystems
{
    public sealed class EventSystem : Behaviour { public static EventSystem current => null; }

    public sealed class StandaloneInputModule : Behaviour { }
}

namespace UnityEngine.UI
{
    using UnityEngine.Events;

    public class Graphic : Behaviour
    {
        public Color color { get; set; }
        public bool raycastTarget { get; set; }
    }

    public class MaskableGraphic : Graphic { }

    public sealed class Image : MaskableGraphic { }

    public enum HorizontalWrapMode { Wrap, Overflow }

    public enum VerticalWrapMode { Truncate, Overflow }

    public sealed class Text : MaskableGraphic
    {
        public Font font { get; set; }
        public string text { get; set; }
        public int fontSize { get; set; }
        public FontStyle fontStyle { get; set; }
        public TextAnchor alignment { get; set; }
        public HorizontalWrapMode horizontalOverflow { get; set; }
        public VerticalWrapMode verticalOverflow { get; set; }
        public bool supportRichText { get; set; }
    }

    public class Selectable : Behaviour { public Graphic targetGraphic { get; set; } public bool interactable { get; set; } }

    public sealed class Button : Selectable
    {
        public sealed class ButtonClickedEvent : UnityEvent { }
        public ButtonClickedEvent onClick { get; set; } = new ButtonClickedEvent();
    }

    public sealed class InputField : Selectable
    {
        public enum LineType { SingleLine, MultiLineSubmit, MultiLineNewline }
        public string text { get; set; }
        public Text textComponent { get; set; }
        public Graphic placeholder { get; set; }
        public int characterLimit { get; set; }
        public LineType lineType { get; set; }
        public void ActivateInputField() { }
    }

    public sealed class CanvasScaler : Behaviour
    {
        public enum ScaleMode { ConstantPixelSize, ScaleWithScreenSize, ConstantPhysicalSize }
        public ScaleMode uiScaleMode { get; set; }
        public Vector2 referenceResolution { get; set; }
        public float matchWidthOrHeight { get; set; }
    }

    public sealed class GraphicRaycaster : Behaviour { }
}

namespace UnityEngine.AI
{
    public sealed class NavMeshAgent : Behaviour
    {
        public float radius { get; set; }
        public float height { get; set; }
        public float speed { get; set; }
        public float acceleration { get; set; }
        public float angularSpeed { get; set; }
        public float stoppingDistance { get; set; }
        public bool isStopped { get; set; }
        public bool isOnNavMesh => true;
        public Vector3 velocity => default;
        public bool SetDestination(Vector3 target) => true;
        public void ResetPath() { }
    }

    public struct NavMeshBuildSettings
    {
        public float agentRadius { get; set; }
        public float agentHeight { get; set; }
        public float agentClimb { get; set; }
        public float agentSlope { get; set; }
    }

    public struct NavMeshBuildSource { }

    public struct NavMeshBuildMarkup
    {
        public bool overrideArea { get; set; }
        public int area { get; set; }
        public Transform root { get; set; }
    }

    public enum NavMeshCollectGeometry { RenderMeshes, PhysicsColliders }

    public sealed class NavMeshData : Object { }

    public struct NavMeshDataInstance { public bool valid => false; public void Remove() { } }

    public static class NavMesh
    {
        public static NavMeshBuildSettings GetSettingsByID(int agentTypeID) => default;
        public static NavMeshDataInstance AddNavMeshData(NavMeshData data) => default;
    }

    public static class NavMeshBuilder
    {
        public static void CollectSources(Transform root, int includedLayerMask, NavMeshCollectGeometry geometry, int defaultArea,
                                          List<NavMeshBuildMarkup> markups, List<NavMeshBuildSource> results) { }
        public static NavMeshData BuildNavMeshData(NavMeshBuildSettings buildSettings, List<NavMeshBuildSource> sources,
                                                   Bounds localBounds, Vector3 position, Quaternion rotation) => null;
        public static bool UpdateNavMeshData(NavMeshData data, NavMeshBuildSettings buildSettings, List<NavMeshBuildSource> sources,
                                             Bounds localBounds) => true;
    }
}

namespace UnityEngine.Rendering
{
    public enum AmbientMode { Skybox, Trilight, Flat, Custom }

    public enum ShadowCastingMode { Off, On, TwoSided, ShadowsOnly }

    public class RenderPipelineAsset : ScriptableObject { }

    public static class GraphicsSettings { public static RenderPipelineAsset defaultRenderPipeline { get; set; } }

    public class VolumeParameter<T> { public void Override(T x) { } }

    public class ClampedFloatParameter : VolumeParameter<float> { }

    public class MinFloatParameter : VolumeParameter<float> { }

    public class FloatParameter : VolumeParameter<float> { }

    public class VolumeComponent : ScriptableObject { }

    public sealed class VolumeProfile : ScriptableObject { public T Add<T>(bool overrides = false) where T : VolumeComponent, new() => new T(); }

    public sealed class Volume : MonoBehaviour
    {
        public bool isGlobal { get; set; }
        public VolumeProfile sharedProfile { get; set; }
    }
}

namespace UnityEngine.Rendering.Universal
{
    public sealed class Bloom : VolumeComponent { public MinFloatParameter intensity = new MinFloatParameter(); public MinFloatParameter threshold = new MinFloatParameter(); }

    public sealed class ColorAdjustments : VolumeComponent { public ClampedFloatParameter saturation = new ClampedFloatParameter(); public FloatParameter postExposure = new FloatParameter(); }

    public enum TonemappingMode { None, Neutral, ACES }

    public sealed class TonemappingModeParameter : VolumeParameter<TonemappingMode> { }

    public sealed class Tonemapping : VolumeComponent { public TonemappingModeParameter mode = new TonemappingModeParameter(); }

    public sealed class Vignette : VolumeComponent { public ClampedFloatParameter intensity = new ClampedFloatParameter(); }

    public sealed class UniversalAdditionalCameraData : MonoBehaviour { public bool renderPostProcessing { get; set; } }

    public static class CameraExtensions { public static UniversalAdditionalCameraData GetUniversalAdditionalCameraData(this Camera camera) => null; }

    public class ScriptableRendererData : ScriptableObject { }

    public sealed class UniversalRendererData : ScriptableRendererData { }

    public sealed class UniversalRenderPipelineAsset : RenderPipelineAsset
    {
        public static UniversalRenderPipelineAsset Create(ScriptableRendererData rendererData = null) => null;
    }
}

namespace UnityEditor
{
    using UnityEngine;

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MenuItem : Attribute { public MenuItem(string itemName) { } }

    public static class AssetDatabase
    {
        public static T LoadAssetAtPath<T>(string path) where T : Object => null;
        public static Object[] LoadAllAssetsAtPath(string path) => new Object[0];
        public static void CreateAsset(Object asset, string path) { }
        public static void SaveAssets() { }
        public static bool IsValidFolder(string path) => true;
        public static string CreateFolder(string parentFolder, string newFolderName) => "";
    }

    public static class EditorUtility
    {
        public static void SetDirty(Object target) { }
        public static bool DisplayDialog(string title, string message, string ok) => true;
        public static void RevealInFinder(string path) { }
    }

    public enum BuildTarget { StandaloneOSX = 2, StandaloneWindows64 = 19, iOS = 9, Android = 13 }
    public enum BuildTargetGroup { Unknown = 0, Standalone = 1, iOS = 4, Android = 7 }

    [Flags]
    public enum BuildOptions { None = 0, Development = 1, AutoRunPlayer = 4 }

    public enum ScriptingImplementation { Mono2x = 0, IL2CPP = 1 }
    public enum IconKind { Any = -1, Application = 0, Settings = 1, Notification = 2, Spotlight = 3, Store = 4 }

    [Flags]
    public enum AndroidArchitecture { None = 0, ARMv7 = 1, ARM64 = 2, X86_64 = 8 }

    public struct BuildPlayerOptions
    {
        public string[] scenes { get; set; }
        public string locationPathName { get; set; }
        public BuildTarget target { get; set; }
        public BuildTargetGroup targetGroup { get; set; }
        public BuildOptions options { get; set; }
    }

    public static class BuildPipeline
    {
        public static UnityEditor.Build.Reporting.BuildReport BuildPlayer(BuildPlayerOptions options) => null;
        public static bool IsBuildTargetSupported(BuildTargetGroup group, BuildTarget target) => true;
    }

    public static class EditorUserBuildSettings
    {
        public static BuildTarget activeBuildTarget => BuildTarget.Android;
        public static bool buildAppBundle { get; set; }
        public static bool SwitchActiveBuildTarget(BuildTargetGroup group, BuildTarget target) => true;
    }

    public enum UIOrientation { Portrait, PortraitUpsideDown, LandscapeRight, LandscapeLeft, AutoRotation }

    public static class PlayerSettings
    {
        public static string productName { get; set; }
        public static string companyName { get; set; }
        public static UIOrientation defaultInterfaceOrientation { get; set; }
        public static bool allowedAutorotateToPortrait { get; set; }
        public static bool allowedAutorotateToPortraitUpsideDown { get; set; }
        public static bool allowedAutorotateToLandscapeLeft { get; set; }
        public static bool allowedAutorotateToLandscapeRight { get; set; }
        public static string bundleVersion { get; set; }
        public static void SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget target, string identifier) { }
        public static void SetScriptingBackend(UnityEditor.Build.NamedBuildTarget target, ScriptingImplementation backend) { }
        public static void SetIcons(UnityEditor.Build.NamedBuildTarget target, Texture2D[] icons, IconKind kind) { }

        public static class Android
        {
            public static AndroidArchitecture targetArchitectures { get; set; }
            public static int bundleVersionCode { get; set; }
        }

        public static class iOS
        {
            public static string buildNumber { get; set; }
            public static bool appleEnableAutomaticSigning { get; set; }
        }
    }

    public sealed class SerializedProperty { public int intValue { get; set; } }

    public sealed class SerializedObject
    {
        public SerializedObject(Object obj) { }
        public SerializedProperty FindProperty(string propertyPath) => null;
        public bool ApplyModifiedProperties() => true;
    }

    public sealed class EditorBuildSettingsScene { public EditorBuildSettingsScene(string path, bool enabled) { } }

    public class AssetImporter : Object { }

    public class AssetPostprocessor
    {
        public string assetPath => "";
        public AssetImporter assetImporter => null;
        public virtual uint GetVersion() => 0;
    }

    public enum ModelImporterMeshCompression { Off, Low, Medium, High }
    public enum ModelImporterNormals { Import, Calculate, None }
    public enum ModelImporterTangents { Import, CalculateLegacy, CalculateLegacyWithSplitTangents, None, CalculateMikk }
    public enum ModelImporterMaterialImportMode { None, ImportStandard, ImportViaMaterialDescription }
    public enum ModelImporterAnimationType { None, Legacy, Generic, Human }

    public sealed class ModelImporter : AssetImporter
    {
        public float globalScale { get; set; }
        public bool useFileScale { get; set; }
        public bool importBlendShapes { get; set; }
        public bool importVisibility { get; set; }
        public bool importCameras { get; set; }
        public bool importLights { get; set; }
        public bool isReadable { get; set; }
        public ModelImporterMeshCompression meshCompression { get; set; }
        public ModelImporterNormals importNormals { get; set; }
        public float normalSmoothingAngle { get; set; }
        public ModelImporterNormals importBlendShapeNormals { get; set; }
        public ModelImporterTangents importTangents { get; set; }
        public bool weldVertices { get; set; }
        public bool generateSecondaryUV { get; set; }
        public ModelImporterMaterialImportMode materialImportMode { get; set; }
        public ModelImporterAnimationType animationType { get; set; }
        public bool importAnimation { get; set; }
        public bool optimizeGameObjects { get; set; }
    }

    public static class EditorBuildSettings { public static EditorBuildSettingsScene[] scenes { get; set; } }
}

namespace UnityEngine.SceneManagement
{
    public struct Scene { }
}

namespace UnityEditor.SceneManagement
{
    using UnityEngine.SceneManagement;

    public enum NewSceneSetup { EmptyScene, DefaultGameObjects }

    public enum NewSceneMode { Single, Additive }

    public static class EditorSceneManager
    {
        public static bool SaveCurrentModifiedScenesIfUserWantsTo() => true;
        public static Scene NewScene(NewSceneSetup setup, NewSceneMode mode) => default;
        public static bool SaveScene(Scene scene, string dstScenePath) => true;
    }
}

namespace UnityEditor.Build
{
    public struct NamedBuildTarget
    {
        public static readonly NamedBuildTarget Unknown = default;
        public static readonly NamedBuildTarget Android = default;
        public static readonly NamedBuildTarget iOS = default;
    }
}

namespace UnityEditor.Build.Reporting
{
    public enum BuildResult { Unknown = 0, Succeeded = 1, Failed = 2, Cancelled = 3 }

    public struct BuildSummary
    {
        public BuildResult result { get; set; }
        public string outputPath { get; set; }
        public ulong totalSize { get; set; }
        public System.TimeSpan totalTime { get; set; }
        public int totalErrors { get; set; }
    }

    public sealed class BuildReport { public BuildSummary summary { get; set; } }
}
