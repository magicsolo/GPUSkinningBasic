

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;

public class SkinMapGenerate : MonoBehaviour
{

    [SerializeField]
    private Animator animator;
    [SerializeField]
    private AnimationClip clip;
    [SerializeField]
    private float clipSlider;
    [SerializeField]
    private SkinnedMeshRenderer skinnedMesh;
    [SerializeField]
    private bool DebugOn = false;
    // Start is called before the first frame update

    private float lastAnimTime;
    private AnimatorController _animatorController;

    private void Start()
    {
        _animatorController = new AnimatorController();
        _animatorController.name = "Ctrl";
        _animatorController.AddLayer("Layer0");
        animator.runtimeAnimatorController = _animatorController;
        var state= _animatorController.layers[0].stateMachine.AddState(clip.name);
        state.motion = clip;
        animator.speed = .0f;
        if (DebugOn)
        {
            Invoke("DebugOnMethod",0.1f);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (clipSlider != lastAnimTime )
        {
            animator.Play(clip.name,0,clipSlider);
        }
    }

    void DebugOnMethod()
    {
        int totalFrame = (int)(clip.length * 60);
        GameObject goParent = new GameObject("parent");
        animator.speed = 1.0f;
        animator.Play(clip.name,0,0f);
        float deltaTime = clip.length / totalFrame;
        for (int i = 0; i < totalFrame; i++)
        {
            animator.Update(deltaTime);
            GameObject go = new GameObject();
            Mesh mesh = new Mesh();
            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            skinnedMesh.BakeMesh(mesh);
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.material = skinnedMesh.material;
            go.transform.position = gameObject.transform.position - (i + 1) * 2 * Vector3.right;
            Quaternion eular = new Quaternion();
            eular.eulerAngles = new Vector3(-90, 0, 0);
            go.transform.rotation = eular;
            go.transform.SetParent(goParent.transform);
        }
        animator.speed = .0f;
    }
}
