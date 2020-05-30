namespace VRTK.Examples
{
    using UnityEngine;

    public class fireetinguisher: VRTK_InteractableObject
    {
        private GameObject smoke;
        private ParticleSystem ps;

        public override void StartUsing(VRTK_InteractUse currentUsingObject = null)
        {
            base.StartUsing(currentUsingObject);
            Debug.Log("startusing");
            ps.Play();
         
        }

        public override void StopUsing(VRTK_InteractUse previousUsingObject = null, bool resetUsingObjectState = true)
        {
            base.StopUsing(previousUsingObject, resetUsingObjectState);
            ps.Stop();
        }

      
        protected void Start()
        {
            smoke =GameObject.Find("PressurisedSteam");
            Debug.Log("start");
            ps = smoke.GetComponent<ParticleSystem>();
            ps.Stop();
        }

        protected override void Update()
        {
            base.Update();
         
        }
    }
}