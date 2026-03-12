using FarEmerald.PlayForge;
using FarEmerald.PlayForge.Examples;
using UnityEngine;

public class TestSequences : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            var seq = TaskSequenceExamples.BranchingDialogue();
            TaskSequenceProcess.Register(seq);
        }
        
        if (Input.GetKeyDown(KeyCode.F2))
        {
            var seq = TaskSequenceExamples.SurvivalBasicRound();
            TaskSequenceProcess.Register(seq);
        }
        
        if (Input.GetKeyDown(KeyCode.F3))
        {
            var seq = TaskSequenceExamples.TimeoutCallbacks();
            TaskSequenceProcess.Register(seq);
        }
        
        if (Input.GetKeyDown(KeyCode.F4))
        {
            var seq = TaskSequenceExamples.RetryPattern();
            TaskSequenceProcess.Register(seq);
        }
        
        if (Input.GetKeyDown(KeyCode.F5))
        {
            var seq = TaskSequenceExamples.MainThreadUI();
            TaskSequenceProcess.Register(seq);
        }
        
        if (Input.GetKeyDown(KeyCode.F6))
        {
            var seq = TaskSequenceExamples.StageRepeatWithRepeat();
            TaskSequenceProcess.Register(seq);
        }
    }
}
