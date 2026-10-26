using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace BallPuzzlePlatformer2D
{

    public class PhysicsObject : MonoBehaviour
    {
        public GameObject m_SmashPrefab;
        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }

        public void Smash()
        {
            GameObject obj = Instantiate(m_SmashPrefab);
            obj.transform.position = transform.position + new Vector3(0, 0, -.1f);
            obj.transform.rotation = transform.rotation;
            Destroy(obj, 5);

            Destroy(gameObject);
        }
    }
}