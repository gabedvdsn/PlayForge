using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FESGameplayAbilitySystem.Demo
{
    public class MoveAround : MonoBehaviour
    {
        public List<Vector3> Movements;
        public float speed;
        
        private Vector3 initial;
        private int index;
        
        // Start is called before the first frame update
        void Start()
        {
            initial = transform.position;
            index = 0;
            StartCoroutine(DoMovement(initial + Movements[index]));
        }

        private IEnumerator DoMovement(Vector3 target)
        {
            while (Vector3.Distance(transform.position, target) > .1f)
            {
                transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
                yield return null;
            }

            index = (index + 1) % Movements.Count;
            StartCoroutine(DoMovement(initial + Movements[index]));

        }
    }
}
