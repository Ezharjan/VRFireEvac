using System;
using System.Collections;
using UnityEngine;

namespace UnitySA.Utility
{
    [Serializable]
    public class LerpCtrlBob
    {
        public float BobDuration;
        public float BobAmount;

        private float m_Offset = 0f;


        // provides the offset that can be used
        public float Offset()
        {
            return m_Offset;
        }


        public IEnumerator DoBobCycle()
        {
            // make the camera move down slightly
            float t = 0f;
            while (t < BobDuration)
            {
                m_Offset = Mathf.Lerp(0f, BobAmount, t / BobDuration);
                t += Time.deltaTime;
                yield return new WaitForFixedUpdate();
            }

            // make it move back to neutral
            t = 0f;
            while (t < BobDuration)
            {
                m_Offset = Mathf.Lerp(BobAmount, 0f, t / BobDuration);
                t += Time.deltaTime;
                yield return new WaitForFixedUpdate();
            }
            m_Offset = 0f;
        }
    }
}
