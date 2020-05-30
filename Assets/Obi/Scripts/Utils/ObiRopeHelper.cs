using UnityEngine;
using System.Collections;

namespace Obi
{
	[RequireComponent(typeof(ObiRope))]
	[RequireComponent(typeof(ObiCatmullRomCurve))]
	public class ObiRopeHelper : MonoBehaviour {

		public ObiSolver solver;
		public ObiRopeSection section;
		public Material material;
		public Transform start;
		public Transform end;
		
		private ObiRope rope;
		private ObiCatmullRomCurve path;
	
		void Start () {
	
			// Get all needed components and interconnect them:
			rope = GetComponent<ObiRope>();
			path = GetComponent<ObiCatmullRomCurve>();
			rope.Solver = solver;
			rope.ropePath = path;	
			rope.Section = section;
			GetComponent<MeshRenderer>().material = material;
			
			// Calculate rope start/end and direction in local space:
			Vector3 localStart = transform.InverseTransformPoint(start.position);
			Vector3 localEnd = transform.InverseTransformPoint(end.position);
			Vector3 direction = (localEnd-localStart).normalized;

			// Generate rope path:
			path.controlPoints.Clear();
			path.controlPoints.Add(localStart-direction);
			path.controlPoints.Add(localStart);
			path.controlPoints.Add(localEnd);
			path.controlPoints.Add(localEnd+direction);

			// Setup the simulation:
			StartCoroutine(Setup());
		}

		IEnumerator Setup(){

			// Generate particles and add them to solver:
			yield return StartCoroutine(rope.GeneratePhysicRepresentationForMesh());
			rope.AddToSolver(null);

			// Fix first and last particle in place:
			rope.invMasses[0] = 0;
			rope.invMasses[rope.UsedParticles-1] = 0;
			Oni.SetParticleInverseMasses(solver.OniSolver,new float[]{0},1,rope.particleIndices[0]);
			Oni.SetParticleInverseMasses(solver.OniSolver,new float[]{0},1,rope.particleIndices[rope.UsedParticles-1]);
		}
		
	}
}
