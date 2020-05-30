using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Obi
{
	
	/**
	 * Rope made of Obi particles. No mesh or topology is needed to generate a physic representation from,
	 * since the mesh is generated procedurally.
	 */
	[ExecuteInEditMode]
	[AddComponentMenu("Physics/Obi/Obi Rope")]
	[RequireComponent(typeof (MeshRenderer))]
	[RequireComponent(typeof (MeshFilter))]
	[RequireComponent(typeof (ObiDistanceConstraints))]
	[RequireComponent(typeof (ObiBendingConstraints))]
	[RequireComponent(typeof (ObiTetherConstraints))]
	[RequireComponent(typeof (ObiPinConstraints))]
	[DisallowMultipleComponent]
	public class ObiRope : ObiActor
	{
		public const float DEFAULT_PARTICLE_MASS = 0.1f;
		public const float MAX_YOUNG_MODULUS = 200.0f; //that of high carbon steel (N/m2);
		public const float MIN_YOUNG_MODULUS = 0.0001f; //that of polymer foam (N/m2);

		/**
		 * How to render the rope.
		 */
		public enum RenderingMode
		{
			ProceduralRope,
			Chain,
			Line
		}
	
		public class CurveFrame{

			public Vector3 position = Vector3.zero;
			public Vector3 tangent = Vector3.forward;
			public Vector3 normal = Vector3.up;
			public Vector3 binormal = Vector3.left;

			public void Reset(){
				position = Vector3.zero;
				tangent = Vector3.forward;
				normal = Vector3.up;
				binormal = Vector3.left;
			}

			public CurveFrame(float twist){
				Quaternion twistQ = Quaternion.AngleAxis(twist,tangent);
				normal = twistQ*normal;
				binormal = twistQ*binormal;
			}

			public void Transport(Vector3 newPosition, Vector3 newTangent, float twist){

				// Calculate delta rotation:
				Quaternion rotQ = Quaternion.FromToRotation(tangent,newTangent);
				Quaternion twistQ = Quaternion.AngleAxis(twist,newTangent);
				Quaternion finalQ = twistQ*rotQ;
				
				// Rotate previous frame axes to obtain the new ones:
				normal = finalQ*normal;
				binormal = finalQ*binormal;
				tangent = newTangent;
				position = newPosition;

			}
		}


		[Tooltip("Amount of additional particles in this rope's pool that can be used to extend its lenght, or to tear it.")]
		public int pooledParticles = 10;

		[Tooltip("Path used to generate the rope.")]
		public ObiCurve ropePath = null;

		[HideInInspector][SerializeField] private ObiRopeSection section = null;		/**< Section asset to be extruded along the rope.*/

		[HideInInspector][SerializeField] private float sectionTwist = 0;				/**< Amount of twist applied to each section, in degrees.*/

		[HideInInspector][SerializeField] private float sectionThicknessScale = 0.8f;	/**< Scales section thickness.*/

		[HideInInspector][SerializeField] private bool thicknessFromParticles = true;	/**< Gets rope thickness from particle radius.*/

		[HideInInspector][SerializeField] private Vector2 uvScale = Vector3.one;		/**< Scaling of uvs along rope.*/

		[HideInInspector][SerializeField] private float uvAnchor = 0;					/**< Normalized position of texture coordinate origin along rope.*/

		[HideInInspector][SerializeField] private bool normalizeV = true;

		[Tooltip("Modulates the amount of particles per lenght unit. 1 means as many particles as needed for the given length/thickness will be used, which"+
				 "can be a lot in very thin and long ropes. Setting values between 0 and 1 allows you to override the amount of particles used.")]
		[Range(0,1)]
		public float resolution = 0.5f;												/**< modulates resolution of particle representation.*/

		[HideInInspector][SerializeField] uint smoothing = 1;						/**< Amount of smoothing applied to the particle representation.*/

		public bool tearable = false;

		[Tooltip("Maximum strain betweeen particles before the spring constraint holding them together would break.")]
		[Delayed]
		public float tearResistanceMultiplier = 1000;

		[HideInInspector] public float[] tearResistance; 	/**< Per-particle tear resistances.*/

		[HideInInspector][SerializeField] private RenderingMode renderMode = RenderingMode.ProceduralRope;

		public List<GameObject> chainLinks = new List<GameObject>();

		[HideInInspector][SerializeField] private Vector3 linkScale = Vector3.one;				/**< Scale of chain links..*/

		[HideInInspector][SerializeField] private bool randomizeLinks = false;

		[HideInInspector] public Mesh ropeMesh;
		[HideInInspector][SerializeField] private List<GameObject> linkInstances;

		public GameObject startPrefab;
		public GameObject endPrefab;	
		public GameObject tearPrefab;	

		[Tooltip("Thickness of the rope, it is equivalent to particle radius.")]
		public float thickness = 0.05f;				/**< Thickness of the rope.*/

		private GameObject[] tearPrefabPool;

		[HideInInspector][SerializeField] private bool closed = false;
		[HideInInspector][SerializeField] private float interParticleDistance = 0;
		[HideInInspector][SerializeField] private float restLength = 0;
		[HideInInspector][SerializeField] private int usedParticles = 0;
		[HideInInspector][SerializeField] private int totalParticles = 0;

		private MeshFilter meshFilter;
		private GameObject startPrefabInstance;
		private GameObject endPrefabInstance;

		private float curveLength = 0;
		private float curveSections = 0;
		private List<Vector4[]> curves = new List<Vector4[]>(); 

		private List<Vector3> vertices = new List<Vector3>();
		private List<Vector3> normals = new List<Vector3>();
		private List<Vector4> tangents = new List<Vector4>();
		private List<Vector2> uvs = new List<Vector2>();
		private List<int> tris = new List<int>();

		public ObiDistanceConstraints DistanceConstraints{
			get{return constraints[Oni.ConstraintType.Distance] as ObiDistanceConstraints;}
		}
		public ObiBendingConstraints BendingConstraints{
			get{return constraints[Oni.ConstraintType.Bending] as ObiBendingConstraints;}
		}
		public ObiTetherConstraints TetherConstraints{
			get{return constraints[Oni.ConstraintType.Tether] as ObiTetherConstraints;}
		}
		public ObiPinConstraints PinConstraints{
			get{return constraints[Oni.ConstraintType.Pin] as ObiPinConstraints;}
		}

		public RenderingMode RenderMode{
			set{
				if (value != renderMode){
					renderMode = value;

					ClearChainLinkInstances();	
					GameObject.DestroyImmediate(ropeMesh);

					GenerateVisualRepresentation();
				}	
			}
			get{return renderMode;}
		} 

		public ObiRopeSection Section{
			set{
				if (value != section){
					section = value;
					GenerateProceduralRopeMesh();
				}	
			}
			get{return section;}
		} 

		public float SectionThicknessScale{
			set{
				if (value != sectionThicknessScale){
					sectionThicknessScale = Mathf.Max(0,value);
					UpdateProceduralRopeMesh();
				}	
			}
			get{return sectionThicknessScale;}
		} 

		public bool ThicknessFromParticles{
			set{
				if (value != thicknessFromParticles){
					thicknessFromParticles = value;
					UpdateVisualRepresentation();
				}	
			}
			get{return thicknessFromParticles;}
		} 

		public float SectionTwist{
			set{
				if (value != sectionTwist){
					sectionTwist = value;
					UpdateVisualRepresentation();
				}	
			}
			get{return sectionTwist;}
		}

		public uint Smoothing{
			set{
				if (value != smoothing){
					smoothing = value;
					UpdateProceduralRopeMesh();
				}	
			}
			get{return smoothing;}
		}

		public Vector3 LinkScale{
			set{
				if (value != linkScale){
					linkScale = value;
					UpdateProceduralChainLinks();
				}	
			}
			get{return linkScale;}
		}

		public Vector2 UVScale{
			set{
				if (value != uvScale){
					uvScale = value;
					UpdateProceduralRopeMesh();
				}	
			}
			get{return uvScale;}
		}

		public float UVAnchor{
			set{
				if (value != uvAnchor){
					uvAnchor = value;
					UpdateProceduralRopeMesh();
				}	
			}
			get{return uvAnchor;}
		}

		public bool NormalizeV{
			set{
				if (value != normalizeV){
					normalizeV = value;
					UpdateProceduralRopeMesh();
				}	
			}
			get{return normalizeV;}
		}

		public bool RandomizeLinks{
			set{
				if (value != randomizeLinks){
					randomizeLinks = value;
					GenerateProceduralChainLinks();
				}	
			}
			get{return randomizeLinks;}
		}

		public float InterparticleDistance{
			get{return interParticleDistance * DistanceConstraints.stretchingScale;}
		}

		public int TotalParticles{
			get{return totalParticles;}
		}

		public int UsedParticles{
			get{return usedParticles;}
			set{
				usedParticles = value;
				pooledParticles = totalParticles-usedParticles;
			}
		}

		public float RestLength{
			get{return restLength;}
			set{restLength = value;}
		}

		public bool Closed{
			get{return closed;}
		}

		public int PooledParticles{
			get{return pooledParticles;}
		}

		public override void Awake()
		{
			base.Awake();

			// Create a new chain liks list. When duplicating a chain, we don't want to
			// use references to the original chain's links!
			linkInstances = new List<GameObject>();
			
			meshFilter = GetComponent<MeshFilter>();
		}
	     
		public void OnValidate(){
			thickness = Mathf.Max(0.0001f,thickness);
			uvAnchor = Mathf.Clamp01(uvAnchor);
			tearResistanceMultiplier = Mathf.Max(0.1f,tearResistanceMultiplier);
			resolution = Mathf.Max(0.0001f,resolution);
	    }

		public override void OnEnable(){
			
			base.OnEnable();
			Camera.onPreCull += RopePreCull;
			GenerateVisualRepresentation();

		}
		
		public override void OnDisable(){
			
			base.OnDisable();
			Camera.onPreCull -= RopePreCull;
			
		}

	    public void RopePreCull(Camera cam)
	    {
			// before this camera culls the scene, grab the camera position and update the mesh.
			if (renderMode == RenderingMode.Line){
				UpdateLineMesh(cam);
			}
	    }

		public override void OnSolverStepEnd(){	

			base.OnSolverStepEnd();

			if (isActiveAndEnabled){
				ApplyTearing();
	
				// breakable pin constraints:
				if (PinConstraints.GetBatches().Count > 0){
					((ObiPinConstraintBatch)PinConstraints.GetBatches()[0]).BreakConstraints();
				}
			}
		}

		public override void OnSolverFrameEnd(){
			
			base.OnSolverFrameEnd();

			UpdateVisualRepresentation();
			
		}
		
		public override void OnDestroy(){
			base.OnDestroy();

			GameObject.DestroyImmediate(ropeMesh);

			ClearChainLinkInstances();
			ClearPrefabInstances();
		}
		
		public override bool AddToSolver(object info){
			
			if (Initialized && base.AddToSolver(info)){

				solver.RequireRenderablePositions();

				return true;
			}
			return false;
		}
		
		public override bool RemoveFromSolver(object info){
			
			if (solver != null)
				solver.RelinquishRenderablePositions();

			return base.RemoveFromSolver(info);
		}
		
		/**
	 	* Generates the particle based physical representation of the rope. This is the initialization method for the rope object
		* and should not be called directly once the object has been created.
	 	*/
		public IEnumerator GeneratePhysicRepresentationForMesh()
		{	
			initialized = false;			
			initializing = true;	
			interParticleDistance = -1;

			RemoveFromSolver(null);

			if (ropePath == null){
				Debug.LogError("Cannot initialize rope. There's no ropePath present. Please provide a spline to define the shape of the rope");
				yield break;
			}

			ropePath.RecalculateSplineLenght(0.00001f,7);
			closed = ropePath.Closed;
			restLength = ropePath.Length;

			usedParticles = Mathf.CeilToInt(restLength/thickness * resolution) + (closed ? 0:1);
			totalParticles = usedParticles + pooledParticles; //allocate extra particles to allow for lenght change and tearing.

			active = new bool[totalParticles];
			positions = new Vector3[totalParticles];
			velocities = new Vector3[totalParticles];
			invMasses  = new float[totalParticles];
			solidRadii = new float[totalParticles];
			phases = new int[totalParticles];
			restPositions = new Vector4[totalParticles];
			tearResistance = new float[totalParticles];
			
			int numSegments = usedParticles - (closed ? 0:1);
			if (numSegments > 0)
				interParticleDistance = restLength/(float)numSegments;
			else 
				interParticleDistance = 0;

			float radius = interParticleDistance * resolution;

			for (int i = 0; i < usedParticles; i++){

				active[i] = true;
				invMasses[i] = 1.0f/DEFAULT_PARTICLE_MASS;
				float mu = ropePath.GetMuAtLenght(interParticleDistance*i);
				positions[i] = transform.InverseTransformPoint(ropePath.transform.TransformPoint(ropePath.GetPositionAt(mu)));
				solidRadii[i] = radius;
				phases[i] = Oni.MakePhase(1,selfCollisions?Oni.ParticlePhase.SelfCollide:0);
				tearResistance[i] = 1;

				if (i % 100 == 0)
					yield return new CoroutineJob.ProgressInfo("ObiRope: generating particles...",i/(float)usedParticles);

			}

			// Initialize basic data for pooled particles:
			for (int i = usedParticles; i < totalParticles; i++){

				active[i] = false;
				invMasses[i] = 1.0f/DEFAULT_PARTICLE_MASS;
				solidRadii[i] = radius;
				phases[i] = Oni.MakePhase(1,selfCollisions?Oni.ParticlePhase.SelfCollide:0);
				tearResistance[i] = 1;

				if (i % 100 == 0)
					yield return new CoroutineJob.ProgressInfo("ObiRope: generating particles...",i/(float)usedParticles);

			}

			DistanceConstraints.Clear();
			ObiDistanceConstraintBatch distanceBatch = new ObiDistanceConstraintBatch(false,false,MIN_YOUNG_MODULUS,MAX_YOUNG_MODULUS);
			DistanceConstraints.AddBatch(distanceBatch);

			for (int i = 0; i < numSegments; i++){

				distanceBatch.AddConstraint(i,(i+1) % (ropePath.Closed ? usedParticles:usedParticles+1),interParticleDistance,1,1);		

				if (i % 500 == 0)
					yield return new CoroutineJob.ProgressInfo("ObiRope: generating structural constraints...",i/(float)numSegments);

			}

			BendingConstraints.Clear();
			ObiBendConstraintBatch bendingBatch = new ObiBendConstraintBatch(false,false,MIN_YOUNG_MODULUS,MAX_YOUNG_MODULUS);
			BendingConstraints.AddBatch(bendingBatch);
			for (int i = 0; i < usedParticles - (closed?0:2); i++){

				// rope bending constraints always try to keep it completely straight:
				bendingBatch.AddConstraint(i,(i+2) % usedParticles,(i+1) % usedParticles,0,0,1);
			
				if (i % 500 == 0)
					yield return new CoroutineJob.ProgressInfo("ObiRope: adding bend constraints...",i/(float)usedParticles);

			}
			
			// Initialize tether constraints:
			TetherConstraints.Clear();

			// Initialize pin constraints:
			PinConstraints.Clear();
			ObiPinConstraintBatch pinBatch = new ObiPinConstraintBatch(false,false,0,MAX_YOUNG_MODULUS);
			PinConstraints.AddBatch(pinBatch);

			initializing = false;
			initialized = true;

			RegenerateRestPositions();
			GenerateVisualRepresentation();
		}

		/**
		 * Generates new valid rest positions for the entire rope.
		 */
		public void RegenerateRestPositions(){

			ObiDistanceConstraintBatch distanceBatch = DistanceConstraints.GetBatches()[0] as ObiDistanceConstraintBatch;		

			// Iterate trough all distance constraints in order:
			int particle = -1;
			int lastParticle = -1;
			float accumulatedDistance = 0;
			for (int i = 0; i < distanceBatch.ConstraintCount; ++i){

				if (i == 0){
					lastParticle = particle = distanceBatch.springIndices[i*2];
					restPositions[particle] = Vector4.zero;
				}		
				
				accumulatedDistance += Mathf.Min(interParticleDistance,solidRadii[particle],solidRadii[lastParticle]);

				particle = distanceBatch.springIndices[i*2+1];
				restPositions[particle] = Vector3.right * accumulatedDistance;
				restPositions[particle][3] = 0; // activate rest position

			}

			PushDataToSolver(ParticleData.REST_POSITIONS);
		}

		/**
		 * Recalculates rest rope length.
		 */
		public void RecalculateLenght(){

			ObiDistanceConstraintBatch distanceBatch = DistanceConstraints.GetBatches()[0] as ObiDistanceConstraintBatch;		

			restLength = 0;

			// Iterate trough all distance constraints in order:
			for (int i = 0; i < distanceBatch.ConstraintCount; ++i)
				restLength += distanceBatch.restLengths[i];
			
		}

		/**
		 * Returns actual rope length, including stretch.
		 */
		public float CalculateLength(){

			ObiDistanceConstraintBatch batch = DistanceConstraints.GetBatches()[0] as ObiDistanceConstraintBatch;

			// iterate over all distance constraints and accumulate their length:
			float actualLength = 0;
			Vector3 a,b;
			for (int i = 0; i < batch.ConstraintCount; ++i){
				a = GetParticlePosition(batch.springIndices[i*2]);
				b = GetParticlePosition(batch.springIndices[i*2+1]);
				actualLength += Vector3.Distance(a,b);
			}
			return actualLength;
		}

		/**
		 * Generates any precomputable data for the current visual representation.
		 */
		public void GenerateVisualRepresentation(){

			// create start/end prefabs
			GeneratePrefabInstances();

			// generate the adequate data for the current visualization (chain/rope):
			if (renderMode != RenderingMode.Chain)
				GenerateProceduralRopeMesh();
			else
				GenerateProceduralChainLinks();
		}

		/**
		 * Updates the current visual representation.
		 */
		public void UpdateVisualRepresentation(){
			if (renderMode != RenderingMode.Chain)
				UpdateProceduralRopeMesh();
			else
				UpdateProceduralChainLinks();
		}	


		/**
		 * Initializes the mesh used to render the rope procedurally.
		 */
		private void GenerateProceduralRopeMesh(){

			if (!initialized)
				return;

			GameObject.DestroyImmediate(ropeMesh);
			ropeMesh = new Mesh();
			ropeMesh.MarkDynamic();
			meshFilter.mesh = ropeMesh;

			UpdateProceduralRopeMesh();
			
		}

		private void GeneratePrefabInstances(){

			ClearPrefabInstances();

			if (tearPrefab != null){

				// create tear prefab pool, two per potential cut:
				tearPrefabPool = new GameObject[pooledParticles*2];

				for (int i = 0; i < tearPrefabPool.Length; ++i){
					GameObject tearPrefabInstance = GameObject.Instantiate(tearPrefab);
					tearPrefabInstance.hideFlags = HideFlags.HideAndDontSave;
					tearPrefabInstance.SetActive(false);
					tearPrefabPool[i] = tearPrefabInstance;
				}

			}

			// create start/end prefabs
			if (startPrefabInstance == null && startPrefab != null){
				startPrefabInstance = GameObject.Instantiate(startPrefab);
				startPrefabInstance.hideFlags = HideFlags.HideAndDontSave;
			}
			if (endPrefabInstance == null && endPrefab != null){
				endPrefabInstance = GameObject.Instantiate(endPrefab);
				endPrefabInstance.hideFlags = HideFlags.HideAndDontSave;
			}
		}

		/**
		 * Destroys all prefab instances used as start/end caps and tear prefabs.
		 */
		private void ClearPrefabInstances(){

			GameObject.DestroyImmediate(startPrefabInstance);
			GameObject.DestroyImmediate(endPrefabInstance);

			if (tearPrefabPool != null){
				for (int i = 0; i < tearPrefabPool.Length; ++i){
					if (tearPrefabPool[i] != null){
						GameObject.DestroyImmediate(tearPrefabPool[i]);
						tearPrefabPool[i] = null;
					}
				}
			}
		}
		
		/**
		 * Analogous to what generate GenerateProceduralRopeMesh does, generates the links used in the chain.
		 */
		public void GenerateProceduralChainLinks(){

			ClearChainLinkInstances();

			if (!initialized)
				return;
			
			if (chainLinks.Count > 0){

				for (int i = 0; i < totalParticles; ++i){
	
					int index = randomizeLinks ? UnityEngine.Random.Range(0,chainLinks.Count) : i % chainLinks.Count;
	
					GameObject linkInstance = null;

					if (chainLinks[index] != null){
						linkInstance = GameObject.Instantiate(chainLinks[index]);
						linkInstance.hideFlags = HideFlags.HideAndDontSave;
						linkInstance.SetActive(false);
					}
	
					linkInstances.Add(linkInstance);
				}
	
			}

			UpdateProceduralChainLinks();
		}

		/**
		 * Destroys all chain link instances. Used when the chain must be re-created from scratch, and when the actor is disabled/destroyed.
		 */
		private void ClearChainLinkInstances(){
			for (int i = 0; i < linkInstances.Count; ++i){
				if (linkInstances[i] != null)
					GameObject.DestroyImmediate(linkInstances[i]);
			}
			linkInstances.Clear();
		}

		/** 
		 * This method uses Chainkin's algorithm to produce a smooth curve from a set of control points. It is specially fast
		 * because it directly calculates subdivision level k, instead of recursively calculating levels 1..k.
		 */

		private Vector4[] ChaikinSmoothing(Vector4[] input, uint k)
		{
			// no subdivision levels, no work to do:
			if (k == 0 || input.Length < 3) 
				return input;

			// calculate amount of new points generated by each inner control point:
			int pCount = (int)Mathf.Pow(2,k);

			// precalculate some quantities:
			int n0 = input.Length-1;
			float twoRaisedToMinusKPlus1 = Mathf.Pow(2,-(k+1));
			float twoRaisedToMinusK = Mathf.Pow(2,-k);
			float twoRaisedToMinus2K = Mathf.Pow(2,-2*k);
			float twoRaisedToMinus2KMinus1 = Mathf.Pow(2,-2*k-1);

			// allocate ouput:
			Vector4[] output = new Vector4[(n0-1) * pCount + 2]; 

			// precalculate coefficients:
			float[] F = new float[pCount];
			float[] G = new float[pCount];
			float[] H = new float[pCount];

			for (int j = 1; j <= pCount; ++j){
				F[j-1] = 0.5f - twoRaisedToMinusKPlus1 - (j-1)*(twoRaisedToMinusK - j*twoRaisedToMinus2KMinus1);
				G[j-1] = 0.5f + twoRaisedToMinusKPlus1 + (j-1)*(twoRaisedToMinusK - j*twoRaisedToMinus2K); 
				H[j-1] = (j-1)*j*twoRaisedToMinus2KMinus1;
			}

			// calculate initial curve points:
			output[0] = (0.5f + twoRaisedToMinusKPlus1) * input[0] + (0.5f - twoRaisedToMinusKPlus1) * input[1];
			output[pCount*n0-pCount+1] = (0.5f - twoRaisedToMinusKPlus1) * input[n0-1] + (0.5f + twoRaisedToMinusKPlus1) * input[n0];

			// calculate internal points:
			for (int i = 1; i < n0; ++i){
				for (int j = 1; j <= pCount; ++j){
					output[(i-1)*pCount+j] = F[j-1]*input[i-1] + G[j-1]*input[i] + H[j-1]*input[i+1];
				}
			}

			return output;
		}	

		private float CalculateCurveLength(Vector4[] curve){
			float length = 0;
			for (int i = 1; i < curve.Length; ++i){
				length += Vector3.Distance(curve[i],curve[i-1]);
			}
			return length;
		}

		/**
		 * Returns the index of the distance constraint at a given normalized rope coordinate.
		 */
		public int GetConstraintIndexAtNormalizedCoordinate(float coord){

			// Nothing guarantees particle index order is the same as particle ordering in the rope.
			// However distance constraints must be ordered, so we'll use that:

			ObiDistanceConstraintBatch distanceBatch = DistanceConstraints.GetBatches()[0] as ObiDistanceConstraintBatch;	
		
			float mu = coord * distanceBatch.ConstraintCount;
			return Mathf.Clamp(Mathf.FloorToInt(mu),0,distanceBatch.ConstraintCount-1);
		}

		/**
		 * Counts the amount of continuous sections in each chunk of rope.
		 */
		private List<int> CountContinuousSections(){

			List<int> sectionCounts = new List<int>(usedParticles);
			ObiDistanceConstraintBatch distanceBatch = DistanceConstraints.GetBatches()[0] as ObiDistanceConstraintBatch;		

			int sectionCount = 0;
			int lastParticle = -1;

			// Iterate trough all distance constraints in order. If we find a discontinuity, reset segment count:
			for (int i = 0; i < distanceBatch.ConstraintCount; ++i){

				int particle1 = distanceBatch.springIndices[i*2];
				int particle2 = distanceBatch.springIndices[i*2+1];
			
				// start new curve at discontinuities:
				if (particle1 != lastParticle && sectionCount > 0){
					sectionCounts.Add(sectionCount);
					sectionCount = 0;
				}

				lastParticle = particle2;
				sectionCount++;
			}

			if (sectionCount > 0)
				sectionCounts.Add(sectionCount);

			return sectionCounts;
		}

		/** 
		 * Generate a list of smooth curves using particles as control points. Will take into account cuts in the rope,
		 * generating one curve for each continuous piece of rope.
		 */
		private void SmoothCurvesFromParticles(){

			curveSections = 0;
			curveLength = 0;
		
			// we will return a list of curves, one for each disjoint rope chunk:
			curves.Clear();

			// count amount of segments in each rope chunk:
			List<int> sectionCounts = CountContinuousSections();

			ObiDistanceConstraintBatch distanceBatch = DistanceConstraints.GetBatches()[0] as ObiDistanceConstraintBatch;	

			Matrix4x4 w2l = transform.worldToLocalMatrix;

			int firstSegment = 0;

			// generate curve for each rope chunk:
			foreach (int sections in sectionCounts){

				// allocate memory for the curve:
				Vector4[] controlPoints = new Vector4[sections+1];

				// get control points position:
				for (int m = 0; m < sections; ++m){

					int particleIndex = distanceBatch.springIndices[(firstSegment + m)*2];
					controlPoints[m] = w2l.MultiplyPoint3x4(GetParticlePosition(particleIndex));
					controlPoints[m].w = solidRadii[particleIndex];	

					// last segment adds its second particle too:
					if (m == sections-1){
						particleIndex = distanceBatch.springIndices[(firstSegment + m)*2+1];
						controlPoints[m+1] = w2l.MultiplyPoint3x4(GetParticlePosition(particleIndex));
						controlPoints[m+1].w = solidRadii[particleIndex];		
					}
				}

				firstSegment += sections;

				// get smooth curve points:
				Vector4[] curve = ChaikinSmoothing(controlPoints,smoothing);

				// make first and last curve points coincide with control points:
				curve[0] = controlPoints[0];	
				curve[curve.Length-1] = controlPoints[controlPoints.Length-1];	

				// store the curve:
				curves.Add(curve);

				// count total curve sections and total curve length:
				curveSections += curve.Length-1;
				curveLength += CalculateCurveLength(curve);
			}
	
		}

		private void PlaceObjectAtCurveFrame(CurveFrame frame, GameObject obj, Space space, bool reverseLookDirection){
			if (space == Space.Self){
				Matrix4x4 l2w = transform.localToWorldMatrix;
				obj.transform.position = l2w.MultiplyPoint3x4(frame.position);
				if (frame.tangent != Vector3.zero)
					obj.transform.rotation = Quaternion.LookRotation(l2w.MultiplyVector(reverseLookDirection ? frame.tangent:-frame.tangent),
																 	 l2w.MultiplyVector(frame.normal));
			}else{
				obj.transform.position = frame.position;
				if (frame.tangent != Vector3.zero)
					obj.transform.rotation = Quaternion.LookRotation(reverseLookDirection ? frame.tangent:-frame.tangent,frame.normal);
			}
		}

		/**
 	 	 * Updates the procedural mesh used to draw a rope based of particle positions.
 	 	 */
		public void UpdateProceduralRopeMesh()
		{
			if (!enabled || ropeMesh == null || section == null) return;

			SmoothCurvesFromParticles();

			if (renderMode == RenderingMode.ProceduralRope){
				UpdateRopeMesh();
			}

		}

		private void ClearMeshData(){
			ropeMesh.Clear();
			vertices.Clear();
			normals.Clear();
			tangents.Clear();
			uvs.Clear();
			tris.Clear();
		}

		private void CommitMeshData(){
			ropeMesh.SetVertices(vertices);
			ropeMesh.SetNormals(normals);
			ropeMesh.SetTangents(tangents);
			ropeMesh.SetUVs(0,uvs);
			ropeMesh.SetTriangles(tris,0,true);
		}

		private void UpdateRopeMesh(){

			ClearMeshData();

			float actualToRestLengthRatio = curveLength/restLength;

			int sectionSegments = section.Segments;
			int verticesPerSection = sectionSegments + 1; 				// the last vertex in each section must be duplicated, due to uv wraparound.

			float vCoord = -uvScale.y * restLength * uvAnchor;	// v texture coordinate.
			int sectionIndex = 0;
			int tearCount = 0;

			// we will define and transport a reference frame along the curve using parallel transport method:
			CurveFrame frame = new CurveFrame(-sectionTwist * curveSections * uvAnchor);

			// for closed curves, last frame of the last curve must be equal to first frame of first curve.
			Vector3 firstTangent = Vector3.forward;

			Vector4 texTangent = Vector4.zero;
			Vector2 uv = Vector2.zero;

			for (int c = 0; c < curves.Count; ++c){
				
				Vector4[] curve = curves[c];

				// Reinitialize frame for each curve.
				frame.Reset();

				for (int i = 0; i < curve.Length; ++i){
	
					// Calculate previous and next curve indices:
					int nextIndex = Mathf.Min(i+1,curve.Length-1);
					int prevIndex = Mathf.Max(i-1,0);
	
					// Calculate current tangent as the vector between previous and next curve points:
					Vector3 nextV;

					// The next tangent of the last segment of the last curve in a closed rope, is the first tangent again:
					if (closed && c == curves.Count-1 && i == curve.Length-1 )
						nextV = firstTangent;
					else 
						nextV = curve[nextIndex] - curve[i];

					Vector3 prevV = curve[i] - curve[prevIndex];
					Vector3 tangent = nextV + prevV;

					// update frame:
					frame.Transport(curve[i],tangent,sectionTwist);

					// update tear prefabs:
					if (tearPrefabPool != null ){

						// first segment of not last first curve:
						if (tearCount < tearPrefabPool.Length && c > 0 && i == 0){
							if (!tearPrefabPool[tearCount].activeSelf)
								tearPrefabPool[tearCount].SetActive(true);
						
							PlaceObjectAtCurveFrame(frame,tearPrefabPool[tearCount],Space.Self, false);
		
							tearCount++;
						}

						// last segment of not last curve:
						if (tearCount < tearPrefabPool.Length && c < curves.Count-1 && i == curve.Length-1){
							if (!tearPrefabPool[tearCount].activeSelf)
								tearPrefabPool[tearCount].SetActive(true);
						
							PlaceObjectAtCurveFrame(frame,tearPrefabPool[tearCount],Space.Self, true);
		
							tearCount++;
						}
					}

					// update start/end prefabs:
					if (c == 0 && i == 0){

						// store first tangent of the first curve (for closed ropes):
						firstTangent = tangent;

						if (startPrefabInstance != null && !closed)
							PlaceObjectAtCurveFrame(frame,startPrefabInstance, Space.Self, false);

					}else if (c == curves.Count-1 && i == curve.Length-1 && endPrefabInstance != null && !closed){
							PlaceObjectAtCurveFrame(frame,endPrefabInstance,Space.Self, true);
					}
		
					// advance v texcoord:
					vCoord += uvScale.y * (Vector3.Distance(curve[i],curve[prevIndex])/ (normalizeV?curveLength:actualToRestLengthRatio));
	
					// calculate section thickness (either constant, or particle radius based):
					float sectionThickness = (thicknessFromParticles ? curve[i].w : thickness) * sectionThicknessScale;

					// Loop around each segment:
					for (int j = 0; j <= sectionSegments; ++j){
	
						vertices.Add(frame.position + (section.vertices[j].x*frame.normal + section.vertices[j].y*frame.binormal) * sectionThickness);
						normals.Add(vertices[vertices.Count-1] - frame.position);
						texTangent = -Vector3.Cross(normals[normals.Count-1],frame.tangent);
						texTangent.w = 1;
						tangents.Add(texTangent);

						uv.Set((j/(float)sectionSegments)*uvScale.x,vCoord);
						uvs.Add(uv);
	
						if (j < sectionSegments && i < curve.Length-1){

							tris.Add(sectionIndex*verticesPerSection + j); 			
							tris.Add(sectionIndex*verticesPerSection + (j+1)); 		
							tris.Add((sectionIndex+1)*verticesPerSection + j); 		
	
							tris.Add(sectionIndex*verticesPerSection + (j+1)); 		
							tris.Add((sectionIndex+1)*verticesPerSection + (j+1)); 	
							tris.Add((sectionIndex+1)*verticesPerSection + j); 		

						}
					}

					sectionIndex++;
				}

			}

			CommitMeshData();
		}

		private void UpdateLineMesh(Camera camera){

			ClearMeshData();

			float actualToRestLengthRatio = curveLength/restLength;

			float vCoord = -uvScale.y * restLength * uvAnchor;	// v texture coordinate.
			int sectionIndex = 0;
			int tearCount = 0;

			Vector3 localSpaceCamera = transform.InverseTransformPoint(camera.transform.position);

			// we will define and transport a reference frame along the curve using parallel transport method:
			CurveFrame frame = new CurveFrame(-sectionTwist * curveSections * uvAnchor);

			// for closed curves, last frame of the last curve must be equal to first frame of first curve.
			Vector3 firstTangent = Vector3.forward;

			Vector4 texTangent = Vector4.zero;
			Vector2 uv = Vector2.zero;

			for (int c = 0; c < curves.Count; ++c){
				
				Vector4[] curve = curves[c];

				// Reinitialize frame for each curve.
				frame.Reset();

				for (int i = 0; i < curve.Length; ++i){
	
					// Calculate previous and next curve indices:
					int nextIndex = Mathf.Min(i+1,curve.Length-1);
					int prevIndex = Mathf.Max(i-1,0);
	
					// Calculate current tangent as the vector between previous and next curve points:
					Vector3 nextV;

					// The next tangent of the last segment of the last curve in a closed rope, is the first tangent again:
					if (closed && c == curves.Count-1 && i == curve.Length-1 )
						nextV = firstTangent;
					else 
						nextV = curve[nextIndex] - curve[i];

					Vector3 prevV = curve[i] - curve[prevIndex];
					Vector3 tangent = nextV + prevV;

					// update frame:
					frame.Transport(curve[i],tangent,sectionTwist);

					// update tear prefabs:
					if (tearPrefabPool != null ){

						// first segment of not last first curve:
						if (tearCount < tearPrefabPool.Length && c > 0 && i == 0){
							if (!tearPrefabPool[tearCount].activeSelf)
								tearPrefabPool[tearCount].SetActive(true);
						
							PlaceObjectAtCurveFrame(frame,tearPrefabPool[tearCount],Space.Self, false);
		
							tearCount++;
						}

						// last segment of not last curve:
						if (tearCount < tearPrefabPool.Length && c < curves.Count-1 && i == curve.Length-1){
							if (!tearPrefabPool[tearCount].activeSelf)
								tearPrefabPool[tearCount].SetActive(true);
						
							PlaceObjectAtCurveFrame(frame,tearPrefabPool[tearCount],Space.Self, true);
		
							tearCount++;
						}
					}

					// update start/end prefabs:
					if (c == 0 && i == 0){

						// store first tangent of the first curve (for closed ropes):
						firstTangent = tangent;

						if (startPrefabInstance != null && !closed)
							PlaceObjectAtCurveFrame(frame,startPrefabInstance, Space.Self, false);

					}else if (c == curves.Count-1 && i == curve.Length-1 && endPrefabInstance != null && !closed){
							PlaceObjectAtCurveFrame(frame,endPrefabInstance,Space.Self, true);
					}
		
					// advance v texcoord:
					vCoord += uvScale.y * (Vector3.Distance(curve[i],curve[prevIndex])/ (normalizeV?curveLength:actualToRestLengthRatio));
	
					// calculate section thickness (either constant, or particle radius based):
					float sectionThickness = (thicknessFromParticles ? curve[i].w : thickness) * sectionThicknessScale;

					Vector3 normal = frame.position - localSpaceCamera;
					normal.Normalize();

					Vector3 bitangent = Vector3.Cross(frame.tangent,normal);
					bitangent.Normalize();

					vertices.Add(frame.position + bitangent * sectionThickness);
					vertices.Add(frame.position - bitangent * sectionThickness);

					normals.Add(-normal);
					normals.Add(-normal);

					texTangent = -bitangent;
					texTangent.w = 1;
					tangents.Add(texTangent);
					tangents.Add(texTangent);

					uv.Set(0,vCoord);
					uvs.Add(uv);
					uv.Set(1,vCoord);
					uvs.Add(uv);

					if (i < curve.Length-1){
						tris.Add(sectionIndex*2); 		
						tris.Add(sectionIndex*2 + 1); 		
						tris.Add((sectionIndex+1)*2); 	
								
						tris.Add(sectionIndex*2 + 1); 	
						tris.Add((sectionIndex+1)*2 + 1); 
						tris.Add((sectionIndex+1)*2);		
					}

					sectionIndex++;
				}

			}

			CommitMeshData();

		}


		/**
		 * Updates chain link positions.
		 */
		public void UpdateProceduralChainLinks(){

			if (linkInstances.Count == 0)
				return;

			ObiDistanceConstraintBatch distanceBatch = DistanceConstraints.GetBatches()[0] as ObiDistanceConstraintBatch;

			// we will define and transport a reference frame along the curve using parallel transport method:
			CurveFrame frame = new CurveFrame(-sectionTwist * distanceBatch.ConstraintCount * uvAnchor);

			int lastParticle = -1;
			int tearCount = 0;

			for (int i = 0; i < distanceBatch.ConstraintCount; ++i){

				int particle1 = distanceBatch.springIndices[i*2];
				int particle2 = distanceBatch.springIndices[i*2+1];

				Vector3 pos = GetParticlePosition(particle1);
				Vector3 nextPos = GetParticlePosition(particle2);
				Vector3 linkVector = nextPos-pos;
				Vector3 tangent = linkVector.normalized;

				if (i > 0 && particle1 != lastParticle){

					// update tear prefab at the first side of tear:
					if (tearPrefabPool != null && tearCount < tearPrefabPool.Length){
						
						if (!tearPrefabPool[tearCount].activeSelf)
							tearPrefabPool[tearCount].SetActive(true);
					
						PlaceObjectAtCurveFrame(frame,tearPrefabPool[tearCount],Space.World, true);
						tearCount++;
					}

					// reset frame at discontinuities:
					frame.Reset();
				}

				// update frame:
				frame.Transport(nextPos,tangent,sectionTwist);

				// update tear prefab at the other side of the tear:
				if (i > 0 && particle1 != lastParticle && tearPrefabPool != null && tearCount < tearPrefabPool.Length){
					
					if (!tearPrefabPool[tearCount].activeSelf)
						tearPrefabPool[tearCount].SetActive(true);

					frame.position = pos;
					PlaceObjectAtCurveFrame(frame,tearPrefabPool[tearCount],Space.World, false);
					tearCount++;
				}

				// update start/end prefabs:
				if (!closed){
					if (i == 0 && startPrefabInstance != null)
						PlaceObjectAtCurveFrame(frame,startPrefabInstance,Space.World,false);
					else if (i == distanceBatch.ConstraintCount-1 && endPrefabInstance != null){
						frame.position = nextPos;
						PlaceObjectAtCurveFrame(frame,endPrefabInstance,Space.World,true);
					}
				}

				if (linkInstances[i] != null){
					linkInstances[i].SetActive(true);
					Transform linkTransform = linkInstances[i].transform;
					linkTransform.position = pos + linkVector * 0.5f;
					linkTransform.localScale = thicknessFromParticles ? (solidRadii[particle1]/thickness) * linkScale : linkScale;
					linkTransform.rotation = Quaternion.LookRotation(tangent,frame.normal);
				}

				lastParticle = particle2;

			}		

			for (int i = distanceBatch.ConstraintCount; i < linkInstances.Count; ++i){
				if (linkInstances[i] != null)
					linkInstances[i].SetActive(false);
			}
			
		}
		
		/**
 		* Resets mesh to its original state.
 		*/
		public override void ResetActor(){
	
			PushDataToSolver(ParticleData.POSITIONS | ParticleData.VELOCITIES);
			
			if (particleIndices != null){
				for(int i = 0; i < particleIndices.Length; ++i){
					solver.renderablePositions[particleIndices[i]] = positions[i];
				}
			}

			UpdateVisualRepresentation();

		}

		private void ApplyTearing(){

			if (!tearable) 
				return;
	
			ObiDistanceConstraintBatch distanceBatch = DistanceConstraints.GetBatches()[0] as ObiDistanceConstraintBatch;
			float[] forces = new float[distanceBatch.ConstraintCount];
			Oni.GetBatchConstraintForces(distanceBatch.OniBatch,forces,distanceBatch.ConstraintCount,0);	
	
			List<int> tearedEdges = new List<int>();
			for (int i = 0; i < forces.Length; i++){
	
				float p1Resistance = tearResistance[distanceBatch.springIndices[i*2]];
				float p2Resistance = tearResistance[distanceBatch.springIndices[i*2+1]];

				// average particle resistances:
				float resistance = (p1Resistance + p2Resistance) * 0.5f * tearResistanceMultiplier;
	
				if (-forces[i] * 1000 > resistance){ // units are kilonewtons.
					tearedEdges.Add(i);
				}
			}
	
			if (tearedEdges.Count > 0){
	
				DistanceConstraints.RemoveFromSolver(null);
				BendingConstraints.RemoveFromSolver(null);
				for(int i = 0; i < tearedEdges.Count; i++)
					Tear(tearedEdges[i]);
				BendingConstraints.AddToSolver(this);
				DistanceConstraints.AddToSolver(this);
	
				// update active bending constraints:
				BendingConstraints.SetActiveConstraints();
	
				// upload active particle list to solver:
				solver.UpdateActiveParticles();
			}
			
		}

		/**
		 * Returns whether a bend constraint affects the two particles referenced by a given distance constraint:
		 */
		public bool DoesBendConstraintSpanDistanceConstraint(ObiDistanceConstraintBatch dbatch, ObiBendConstraintBatch bbatch, int d, int b){

		return (bbatch.bendingIndices[b*3+2] == dbatch.springIndices[d*2] &&
			 	bbatch.bendingIndices[b*3+1] == dbatch.springIndices[d*2+1]) ||

			   (bbatch.bendingIndices[b*3+1] == dbatch.springIndices[d*2] &&
			 	bbatch.bendingIndices[b*3+2] == dbatch.springIndices[d*2+1]) ||

			   (bbatch.bendingIndices[b*3+2] == dbatch.springIndices[d*2] &&
			 	bbatch.bendingIndices[b*3] == dbatch.springIndices[d*2+1]) ||

			   (bbatch.bendingIndices[b*3] == dbatch.springIndices[d*2] &&
			 	bbatch.bendingIndices[b*3+2] == dbatch.springIndices[d*2+1]);
		}	

		public void Tear(int constraintIndex){

			// don't allow splitting if there are no free particles left in the pool.
			if (usedParticles >= totalParticles) return;
	
			// get involved constraint batches: 
			ObiDistanceConstraintBatch distanceBatch = (ObiDistanceConstraintBatch)DistanceConstraints.GetBatches()[0];
			ObiBendConstraintBatch bendingBatch = (ObiBendConstraintBatch)BendingConstraints.GetBatches()[0];
	
			// get particle indices at both ends of the constraint:
			int splitIndex = distanceBatch.springIndices[constraintIndex*2];
			int intactIndex = distanceBatch.springIndices[constraintIndex*2+1];

			// see if the rope is continuous at the split index and the intact index:
			bool continuousAtSplit = (constraintIndex < distanceBatch.ConstraintCount-1 && distanceBatch.springIndices[(constraintIndex+1)*2] == splitIndex) || 
									 (constraintIndex > 0 && distanceBatch.springIndices[(constraintIndex-1)*2+1] == splitIndex);

			bool continuousAtIntact = (constraintIndex < distanceBatch.ConstraintCount-1 && distanceBatch.springIndices[(constraintIndex+1)*2] == intactIndex) || 
									  (constraintIndex > 0 && distanceBatch.springIndices[(constraintIndex-1)*2+1] == intactIndex);
	
			// we will split the particle with higher mass, so swap them if needed (and possible). Also make sure that the rope hasnt been cut there yet:
			if ((invMasses[splitIndex] > invMasses[intactIndex] || invMasses[splitIndex] == 0) &&
				continuousAtIntact){

				int aux = splitIndex;
				splitIndex = intactIndex;
				intactIndex = aux;

			} 

			// see if we are able to proceed with the cut:
			if (invMasses[splitIndex] == 0 || !continuousAtSplit){	
				return;
			}

			// halve the mass of the teared particle:
			invMasses[splitIndex] *= 2;

			// copy the new particle data in the actor and solver arrays:
			positions[usedParticles] = positions[splitIndex];
			velocities[usedParticles] = velocities[splitIndex];
			active[usedParticles] = active[splitIndex];
			invMasses[usedParticles] = invMasses[splitIndex];
			solidRadii[usedParticles] = solidRadii[splitIndex];
			phases[usedParticles] = phases[splitIndex];
			tearResistance[usedParticles] = tearResistance[splitIndex];
			restPositions[usedParticles] = positions[splitIndex];
			restPositions[usedParticles][3] = 0; // activate rest position.
			
			// update solver particle data:
			Vector4[] velocity = {Vector4.zero};
			Oni.GetParticleVelocities(solver.OniSolver,velocity,1,particleIndices[splitIndex]);
			Oni.SetParticleVelocities(solver.OniSolver,velocity,1,particleIndices[usedParticles]);
	
			Vector4[] position = {Vector4.zero};
			Oni.GetParticlePositions(solver.OniSolver,position,1,particleIndices[splitIndex]);
			Oni.SetParticlePositions(solver.OniSolver,position,1,particleIndices[usedParticles]);
			
			Oni.SetParticleInverseMasses(solver.OniSolver,new float[]{invMasses[splitIndex]},1,particleIndices[usedParticles]);
			Oni.SetParticleSolidRadii(solver.OniSolver,new float[]{solidRadii[splitIndex]},1,particleIndices[usedParticles]);
			Oni.SetParticlePhases(solver.OniSolver,new int[]{phases[splitIndex]},1,particleIndices[usedParticles]);

			// Update bending constraints:
			for (int i = 0 ; i < bendingBatch.ConstraintCount; ++i){

				// disable the bending constraint centered at the split particle:
				if (bendingBatch.bendingIndices[i*3+2] == splitIndex)
					bendingBatch.DeactivateConstraint(i);

				// update the one that bridges the cut:
				else if (!DoesBendConstraintSpanDistanceConstraint(distanceBatch,bendingBatch,constraintIndex,i)){

					// if the bend constraint does not involve the split distance constraint, 
					// update the end that references the split vertex:
					if (bendingBatch.bendingIndices[i*3] == splitIndex)
						bendingBatch.bendingIndices[i*3] = usedParticles;
					else if (bendingBatch.bendingIndices[i*3+1] == splitIndex)
						bendingBatch.bendingIndices[i*3+1] = usedParticles;

				}
			}

			// Update distance constraints at both ends of the cut:
			if (constraintIndex < distanceBatch.ConstraintCount-1){
				if (distanceBatch.springIndices[(constraintIndex+1)*2] == splitIndex)
					distanceBatch.springIndices[(constraintIndex+1)*2] = usedParticles;
				if (distanceBatch.springIndices[(constraintIndex+1)*2+1] == splitIndex)
					distanceBatch.springIndices[(constraintIndex+1)*2+1] = usedParticles;
			}	

			if (constraintIndex > 0){
				if (distanceBatch.springIndices[(constraintIndex-1)*2] == splitIndex)
					distanceBatch.springIndices[(constraintIndex-1)*2] = usedParticles;
				if (distanceBatch.springIndices[(constraintIndex-1)*2+1] == splitIndex)
					distanceBatch.springIndices[(constraintIndex-1)*2+1] = usedParticles;
			}

			usedParticles++;
			pooledParticles--;

		}

		/**
		 * Automatically generates tether constraints for the cloth.
		 * Partitions fixed particles into "islands", then generates up to maxTethers constraints for each 
		 * particle, linking it to the closest point in each island.
		 */
		public override bool GenerateTethers(TetherType type){
			
			if (!Initialized) return false;
	
			TetherConstraints.Clear();
			
			if (type == TetherType.Hierarchical)
				GenerateHierarchicalTethers(5);
			else
				GenerateFixedTethers(2);
	        
	        return true;
	        
		}

		private void GenerateFixedTethers(int maxTethers){

			ObiTetherConstraintBatch tetherBatch = new ObiTetherConstraintBatch(true,false,MIN_YOUNG_MODULUS,MAX_YOUNG_MODULUS);
			TetherConstraints.AddBatch(tetherBatch);
			
			List<HashSet<int>> islands = new List<HashSet<int>>();
		
			// Partition fixed particles into islands:
			for (int i = 0; i < usedParticles; i++){

				if (invMasses[i] > 0 || !active[i]) continue;
				
				int assignedIsland = -1;
	
				// keep a list of islands to merge with ours:
				List<int> mergeableIslands = new List<int>();
					
				// See if any of our neighbors is part of an island:
				int prev = Mathf.Max(i-1,0);
				int next = Mathf.Min(i+1,usedParticles-1);
		
				for(int k = 0; k < islands.Count; ++k){

					if ((active[prev] && islands[k].Contains(prev)) || 
						(active[next] && islands[k].Contains(next))){

						// if we are not in an island yet, pick this one:
						if (assignedIsland < 0){
							assignedIsland = k;
                            islands[k].Add(i);
						}
						// if we already are in an island, we will merge this newfound island with ours:
						else if (assignedIsland != k && !mergeableIslands.Contains(k)){
							mergeableIslands.Add(k);
						}
					}
                }
				
				// merge islands with the assigned one:
				foreach(int merge in mergeableIslands){
					islands[assignedIsland].UnionWith(islands[merge]);
				}
	
				// remove merged islands:
				mergeableIslands.Sort();
				mergeableIslands.Reverse();
				foreach(int merge in mergeableIslands){
					islands.RemoveAt(merge);
				}
				
				// If no adjacent particle is in an island, create a new one:
				if (assignedIsland < 0){
					islands.Add(new HashSet<int>(){i});
				}
			}	
			
			// Generate tether constraints:
			for (int i = 0; i < usedParticles; ++i){
			
				if (invMasses[i] == 0) continue;
				
				List<KeyValuePair<float,int>> tethers = new List<KeyValuePair<float,int>>(islands.Count);
				
				// Find the closest particle in each island, and add it to tethers.
				foreach(HashSet<int> island in islands){
					int closest = -1;
					float minDistance = Mathf.Infinity;
					foreach (int j in island){

						// TODO: Use linear distance along the rope in a more efficient way. precalculate it on generation!
						int min = Mathf.Min(i,j);
						int max = Mathf.Max(i,j);
						float distance = 0;
						for (int k = min; k < max; ++k)
							distance += Vector3.Distance(positions[k],
														 positions[k+1]);

						if (distance < minDistance){
							minDistance = distance;
							closest = j;
						}
					}
					if (closest >= 0)
						tethers.Add(new KeyValuePair<float,int>(minDistance, closest));
				}
				
				// Sort tether indices by distance:
				tethers.Sort(
				delegate(KeyValuePair<float,int> x, KeyValuePair<float,int> y)
				{
					return x.Key.CompareTo(y.Key);
				}
				);
				
				// Create constraints for "maxTethers" closest anchor particles:
				for (int k = 0; k < Mathf.Min(maxTethers,tethers.Count); ++k){
					tetherBatch.AddConstraint(i,tethers[k].Value,tethers[k].Key,1,1);
				}
			}

			tetherBatch.Cook();
		}

		private void GenerateHierarchicalTethers(int maxLevels){

			ObiTetherConstraintBatch tetherBatch = new ObiTetherConstraintBatch(true,false,MIN_YOUNG_MODULUS,MAX_YOUNG_MODULUS);
			TetherConstraints.AddBatch(tetherBatch);

			// for each level:
			for (int i = 1; i <= maxLevels; ++i){

				int stride = i*2;

				// for each particle:
				for (int j = 0; j < usedParticles - stride; ++j){

					int nextParticle = j + stride;

					tetherBatch.AddConstraint(j,nextParticle % usedParticles,interParticleDistance * stride,1,1);	

				}	
			}

			tetherBatch.Cook();
		
		}
		
	}
}



