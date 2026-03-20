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
            var data = SequenceDataPacket.RootDefault();
            data.SetPrimary(Tags.DATA, Data);
            TaskSequenceProcess.Register(seq, data);
        }
        
        if (Input.GetKeyDown(KeyCode.F2))
        {
            var seq = TaskSequenceExamples.KunkkaTorrentStorm();
            TaskSequenceProcess.Register(seq);
        }
        
        if (Input.GetKeyDown(KeyCode.F3))
        {
            var seq = TaskSequenceExamples.OnHitStackProc();
            TaskSequenceProcess.Register(seq);
        }
        
        if (Input.GetKeyDown(KeyCode.F4))
        {
            var seq = TaskSequenceExamples.TpScroll();
            TaskSequenceProcess.Register(seq);
        }
        
        if (Input.GetKeyDown(KeyCode.F5))
        {
            var seq = TaskSequenceExamples.TwoPartAbility();
            TaskSequenceProcess.Register(seq);
        }
        
        if (Input.GetKeyDown(KeyCode.F6))
        {
            var seq = TaskSequenceExamples.DeathAndRespawn();
            TaskSequenceProcess.Register(seq);
        }
    }
}
