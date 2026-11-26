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
        public static implicit operator bool(Object o) => !ReferenceEquals(o, null);
    }

    public enum FindObjectsSortMode { None, InstanceID }

    public class Component : Object
    {
        public Transform transform => null;
        public GameObject gameObject => null;
        public T GetComponent<T>() => default;
        public T GetComponentInParent<T>() => default;
    }

    public class Behaviour : Component { public bool enabled { get; set; } }

    public class MonoBehaviour : Behaviour { }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new() => new T();
    }

    public sealed class GameObject : Object
    {
        public GameObject() { }
        public GameObject(string name) { }
        public Transform transform => null;
        public string tag { get; set; }
        public T AddComponent<T>() where T : Component => default;
        public T GetComponent<T>() => default;
        public static GameObject CreatePrimitive(PrimitiveType type) => new GameObject();
    }

    public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad }

    public class Transform : Component
    {
        public Vector3 position { get; set; }
        public Vector3 localPosition { get; set; }
        public Quaternion rotation { get; set; }
        public Quaternion localRotation { get; set; }
        public Vector3 localScale { get; set; }
        public void SetParent(Transform parent, bool worldPositionStays) { }
        public void SetPositionAndRotation(Vector3 position, Quaternion rotation) { }
        public Vector3 TransformPoint(Vector3 position) => position;
    }

    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
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
    }

    public struct Color32 { public byte r, g, b, a; }

    public static class ColorUtility { public static bool TryParseHtmlString(string htmlString, out Color color) { color = default; return true; } }

    public static class Mathf
    {
        public const float PI = 3.14159265f;
        public static float Sin(float f) => 0f;
        public static float Cos(float f) => 0f;
        public static float Abs(float f) => 0f;
        public static float Exp(float f) => 0f;
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
    }

    public sealed class Shader : Object { public static Shader Find(string name) => null; }

    public class Renderer : Component { public Material sharedMaterial { get; set; } }

    public static class Resources { public static T Load<T>(string path) where T : Object => null; }

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
        public static double unscaledTimeAsDouble => 0.0;
    }

    public enum TouchPhase { Began, Moved, Stationary, Ended, Canceled }

    public struct Touch { public Vector2 position { get; set; } public TouchPhase phase { get; set; } }

    public enum KeyCode { None, Alpha1, Alpha2, Alpha3, N }

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

    public static class Screen { public static float dpi => 0f; public static ScreenOrientation orientation { get; set; } }

    public static class Application { public static string persistentDataPath => ""; public static bool isMobilePlatform => false; }

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

    public struct Bounds { public Bounds(Vector3 center, Vector3 size) { } }

    public class Collider : Component { }

    public sealed class BoxCollider : Collider { public Vector3 size { get; set; } public Vector3 center { get; set; } }

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
    }
}

namespace UnityEngine.Rendering
{
    public enum AmbientMode { Skybox, Trilight, Flat, Custom }

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
    }

    public sealed class SerializedProperty { public int intValue { get; set; } }

    public sealed class SerializedObject
    {
        public SerializedObject(Object obj) { }
        public SerializedProperty FindProperty(string propertyPath) => null;
        public bool ApplyModifiedProperties() => true;
    }

    public sealed class EditorBuildSettingsScene { public EditorBuildSettingsScene(string path, bool enabled) { } }

    public static class EditorBuildSettings { public static EditorBuildSettingsScene[] scenes { get; set; } }
}

namespace UnityEditor.SceneManagement
{
    public enum NewSceneSetup { EmptyScene, DefaultGameObjects }

    public enum NewSceneMode { Single, Additive }

    public struct Scene { }

    public static class EditorSceneManager
    {
        public static bool SaveCurrentModifiedScenesIfUserWantsTo() => true;
        public static Scene NewScene(NewSceneSetup setup, NewSceneMode mode) => default;
        public static bool SaveScene(Scene scene, string dstScenePath) => true;
    }
}
