
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

public class GPUSkinningBake : EditorWindow
{
    [SerializeField]
    private GameObject prefab;
    [SerializeField]
    private GameObject previousPrefab;
    [SerializeField]
    private List<AnimationClip> animClips = new List<AnimationClip>();
    [FormerlySerializedAs("_skinnedMeshRenderer")] [SerializeField]
    private SkinnedMeshRenderer _skinnedRenderer;
    [SerializeField]
    private Object outPutFolder;
    [SerializeField]
    private Animator _animator;
    [SerializeField]
    private RuntimeAnimatorController _animController;
    [SerializeField]
    private Avatar animAvatar;

    private List<AnimationClip> _clipCache = new List<AnimationClip>();

    private static GPUSkinningBake window;
    private static GPUSkinningBake Instance;
    private Dictionary<string, bool> bakeAnims = new Dictionary<string, bool>();

    [MenuItem("GUISkinning/烘焙动画贴图")]
    static void MakeWindow()
    {
        window = GetWindow(typeof(GPUSkinningBake)) as GPUSkinningBake;
        if (window.prefab != Selection.activeGameObject)
        {
            window.prefab = null;
            window.OnEnable();
        }
            
    }

    private void OnEnable()
    {
        Instance = this;
        titleContent = new GUIContent("烘焙贴图");
        //TODO 啥意思 为什么能这么写 activeGameObject是啥
        if (prefab == null && Selection.activeGameObject)
        {
            prefab = Selection.activeGameObject;
            OnPrefabChange();
        }
    }

    private void OnGUI()
    {
        //TODO GUI.skin.label.wordWrap啥意思
        GUI.skin.label.wordWrap = true;

        using (new EditorGUILayout.HorizontalScope())
            prefab = EditorGUILayout.ObjectField("烘焙预制体",prefab,typeof(GameObject),true) as GameObject;
        if (prefab == null)
            DrawWarning("需要指定烘焙的预制体");
        else if (previousPrefab != prefab)
            OnPrefabChange();

        if (prefab != null && !string.IsNullOrEmpty(GetPrefabPath()))
        {
            //todo 记一下，文件夹可以以这种方式序列化
            outPutFolder = EditorGUILayout.ObjectField("Output Folder", outPutFolder, typeof(Object), false);
        }
        GUILayout.Space(1);

        using (new GUILayout.ScrollViewScope(new Vector2()))
        {
            GUILayout.Label("<b>要烘焙的动画</b>");
            for (int i = 0; i < animClips.Count; i++)
            {
                GUILayout.BeginHorizontal();
                {
                    var previous = animClips[i];
                    animClips[i] = (AnimationClip)EditorGUILayout.ObjectField(animClips[i], typeof(AnimationClip), false);
                    if (GUILayout.Button("删除",GUILayout.Width(32)))
                    {
                        animClips.RemoveAt(i);
                        GUILayout.EndHorizontal();
                        break;
                    }
                }
                GUILayout.EndHorizontal();
            }

            if (GUILayout.Button("添加动画"))
            {
                animClips.Add(null);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (animAvatar == null)
                    GetAvatar();
                animAvatar = EditorGUILayout.ObjectField("骨骼", animAvatar, typeof(Avatar),true) as Avatar;
            }
        }

        if (prefab!=null)
        {
            GUILayout.Space(10);
            int bakeCount = animClips.Count(q => q != null);
            GUI.enabled = bakeCount > 0;
            var c = GUI.color;
            GUI.color = new Color(128 / 255f, 234 / 255f, 255 / 255f, 1);
            if (GUILayout.Button(string.Format("烘焙{0}份动画",bakeCount),GUILayout.Height(30)))
            {
                CreateGPUSkiningTexture();
            }
        }
    }

    private void CreateGPUSkiningTexture()
    {
        RuntimeAnimatorController bakeController = null;
        try
        {
            string assetPath = GetPrefabPath();
            if (string.IsNullOrEmpty(assetPath))
            {
                EditorUtility.DisplayDialog("GPUSkinning", string.Format("无法获取{0}的路径", prefab.name), "OK");
                return;
            }

            if (outPutFolder == null)
            {
                EditorUtility.DisplayDialog("GPSkinning", "无法加载导出路径，请确保路径正确", "OK");
                return;
            }

            string assetFolder = AssetDatabase.GetAssetPath(outPutFolder);
            if (string.IsNullOrEmpty(assetFolder))
            {
                EditorUtility.DisplayDialog("GPUUSkinning", "无法加载到处文件夹", "OK");
                return;
            }

            int animCount = 0;
            GameObject sampleGO = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            _skinnedRenderer = sampleGO.GetComponent<SkinnedMeshRenderer>();
            if (_skinnedRenderer == null)
            {
                _skinnedRenderer = sampleGO.GetComponentInChildren<SkinnedMeshRenderer>();
            }

            if (_skinnedRenderer == null)
            {
                DestroyImmediate(sampleGO);
                throw new System.Exception("预制体没有SkinnedMeshRenderer");
            }
            else
            {
                _animator = sampleGO.GetComponent<Animator>();
                if (_animator == null)
                    _animator = sampleGO.GetComponentInChildren<Animator>();
                if (_animator == null)
                    _animator = sampleGO.AddComponent<Animator>();
                bakeController = CreateBakeController();
                _animator.runtimeAnimatorController = bakeController;
                _animator.avatar = GetAvatar();
                _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                GameObject asset = new GameObject(prefab.name + "_GPUSkinning");
                int vertexCount = 0;
                Transform rootMotionBaker = new GameObject().transform;

                for (int i = 0; i < animClips.Count; ++i)
                {
                    AnimationClip animClip = animClips[i];
                    int bakeFrames = Mathf.CeilToInt(animClips.Count * 60);
                    float lastFrameTime = 0;
                    List<List<Vector3>> framePositions = new List<List<Vector3>>();
                    List<List<Vector3>> frameNormals = new List<List<Vector3>>();
                    for (int j = 0; j < bakeFrames; j++)
                    {
                        float bakeDelta = Mathf.Clamp01((float)j / bakeFrames);
                        EditorUtility.DisplayCancelableProgressBar("烘培贴图", $"烘焙动画：{animClip.name} 第{j}帧", bakeDelta);
                        float animationTime = bakeDelta * animClip.length;
                        if (animClip.isHumanMotion || !animClip.legacy)
                        {
                            float normalizedTime = animationTime / animClip.length;
                            string stateName = animClip.name;
                            _animator.Play(stateName, 0, normalizedTime);
                            if (lastFrameTime == 0)
                            {
                                float nextBakeDelta = Mathf.Clamp01(((float)(j + 1) / bakeFrames));
                                float nextAnimationTime = nextBakeDelta * animClip.length;
                                lastFrameTime = animationTime - nextAnimationTime;
                            }

                            _animator.Update(animationTime - lastFrameTime);
                            lastFrameTime = animationTime;
                        }
                        else
                        {
                            GameObject sampleObject = sampleGO;
                            Animation legacyAnimation = sampleObject.GetComponentInChildren<Animation>();
                            if (_animator && _animator.gameObject != sampleObject)
                                sampleObject = _animator.gameObject;
                            else if (legacyAnimation && legacyAnimation.gameObject != sampleObject)
                                sampleObject = legacyAnimation.gameObject;
                            animClip.SampleAnimation(sampleObject, animationTime);
                        }

                        Mesh skinnedMesh = new Mesh();
                        _skinnedRenderer.BakeMesh(skinnedMesh);
                        Vector3[] meshesInFrame = skinnedMesh.vertices;
                        Vector3[] normalsInFrame = skinnedMesh.normals;
                        rootMotionBaker.position = _animator.rootPosition;
                        rootMotionBaker.rotation = _animator.rootRotation;
                        for (int k = 0; k < meshesInFrame.Length; k++)
                        {
                            meshesInFrame[k] = rootMotionBaker.TransformPoint(meshesInFrame[k]);
                        }

                        framePositions.Add(meshesInFrame.ToList());
                        frameNormals.Add(normalsInFrame.ToList());
                        vertexCount = meshesInFrame.Length;
                        DestroyImmediate(skinnedMesh);
                    }

                    string name = string.Format($"{assetFolder}/{FormatClipName(animClip.name)}_GPUSkinningAsset.asset");
                    GPUSkinningAsset gpuAsset = ScriptableObject.CreateInstance<GPUSkinningAsset>();
                    AssetDatabase.CreateAsset(gpuAsset, name);
                    gpuAsset.CreateBreakedAssets(name, framePositions, frameNormals, animClip.length);
                    animCount++;
                }

                DestroyImmediate(rootMotionBaker.gameObject);
                DestroyImmediate(asset);
            }

            DestroyImmediate(sampleGO);
            EditorUtility.ClearProgressBar();
        }
        catch (Exception e)
        {
            Debug.LogError(e);
        }
        finally
        {
            if (bakeController)
            {
                
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    private string FormatClipName(string name)
    {
        string badChars = "!@#$%%^&*()=+}{[]'\";:|";
        for (int i = 0;i <badChars.Length;i++)
        {
            name = name.Replace(badChars[i], '_');
        }

        return name;
    }
    private RuntimeAnimatorController CreateBakeController()
    {
        AnimatorController controller = new AnimatorController();
        controller.name = "AnimationCtrl";
        controller.AddLayer("Layer0");
        AnimatorStateMachine baseStateMatchine = controller.layers[0].stateMachine;
        foreach (var clip in animClips)
        {
            var state = baseStateMatchine.AddState(clip.name);
            state.motion = clip;
        }

        return controller;
    }

    private string GetPrefabPath()
    {
        string assetPath = AssetDatabase.GetAssetPath(prefab);
        if (string.IsNullOrEmpty(assetPath))
        {
            Object parentObject = PrefabUtility.GetCorrespondingObjectFromSource(prefab);
            assetPath = AssetDatabase.GetAssetPath(parentObject);
        }

        return assetPath;
    }

    private Avatar GetAvatar()
    {
        if (animAvatar)
            return animAvatar;
        //拿到的Object都是些什么
        var objs = EditorUtility.CollectDependencies(new Object[] { prefab }).ToList();
        foreach (var obj in objs.ToArray())
            objs.AddRange(AssetDatabase.LoadAllAssetRepresentationsAtPath(AssetDatabase.GetAssetPath(obj)));

        objs.RemoveAll(q => q is Avatar == false || q == null);
        if (objs.Count > 0)
            animAvatar = objs[0] as Avatar;

        return animAvatar;
    }

    private void DrawWarning(string text)
    {
        int w = (int)Mathf.Lerp(300, 900, text.Length / 200f);
        using (new EditorGUILayout.HorizontalScope(GUILayout.MinHeight(30)))
        {
            //TODO 什么是CNEntryWarnIcon 这里Style是怎么工作的
            var style = new GUIStyle(GUI.skin.FindStyle("CN EntryWarnIcon"));
            style.margin = new RectOffset();
            style.contentOffset = new Vector2();
            GUILayout.Box("",style,GUILayout.Width(15),GUILayout.Height(15));
            var textStyle = new GUIStyle(GUI.skin.label);
            textStyle.contentOffset = new Vector2(10, Instance.position.width < w ? 0 : 5);
            GUILayout.Label(text,textStyle);
        }
    }

    private void OnPrefabChange()
    {
        if (Application.isPlaying)
        {   
            return;
        }

        _animator = null;
        animAvatar = null;
        if (prefab!= null)
        {
            bakeAnims.Clear();  
        }

        previousPrefab = prefab;
    }

}
