using System;
using UnityEngine;

namespace Seino.DynamicBone
{
    public class Spawner : MonoBehaviour
    {
        public int Row = 20;
        public int Col = 20;
        public GameObject Prefab;
        public GameObject Root;

        private void Start()
        {
            for (int i = 0; i < Row; i++)
            {
                for (int j = 0; j < Col; j++)
                {
                    var go = Instantiate(Prefab, new Vector3(i, 0, j), Quaternion.identity);

                    // go.transform.SetParent(Root.transform);
                    go.SetActive(true);
                }
            }
            
        }
    }
}