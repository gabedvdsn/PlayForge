using FarEmerald.PlayForge;
using FarEmerald.PlayForge.Examples;
using UnityEngine;

public class TestSequences : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    public GameObject Data;

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            var seq = TaskSequenceExamples.CharacterChangeCameraEffect();
            var data = SequenceDataPacket.SceneRoot();
            data.SetPrimary(Tags.DATA, Data);
            ProcessControl.Register(seq, data, out _);
        }
        
        if (Input.GetKeyDown(KeyCode.F2))
        {
            var seq = TaskSequenceExamples.KunkkaTorrentStorm();
            ProcessControl.Register(seq, out _);
        }
        
        if (Input.GetKeyDown(KeyCode.F3))
        {
            var seq = TaskSequenceExamples.OnHitStackProc();
            ProcessControl.Register(seq, out _);
        }
        
        if (Input.GetKeyDown(KeyCode.F4))
        {
            var seq = TaskSequenceExamples.TpScroll();
            ProcessControl.Register(seq, out _);
        }
        
        if (Input.GetKeyDown(KeyCode.F5))
        {
            var seq = TaskSequenceExamples.TwoPartAbility();
            ProcessControl.Register(seq, out _);
        }
        
        if (Input.GetKeyDown(KeyCode.F6))
        {
            var seq = TaskSequenceExamples.DeathAndRespawn();
            ProcessControl.Register(seq, out _);
        }
    }
}
