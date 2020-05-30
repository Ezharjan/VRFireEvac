using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Obi{
	
	/**
	 * Custom inspector for ObiRope components.
	 * Allows particle selection and constraint edition. 
	 * 
	 * Selection:
	 * 
	 * - To select a particle, left-click on it. 
	 * - You can select multiple particles by holding shift while clicking.
	 * - To deselect all particles, click anywhere on the object except a particle.
	 * 
	 * Constraints:
	 * 
	 * - To edit particle constraints, select the particles you wish to edit.
	 * - Constraints affecting any of the selected particles will appear in the inspector.
	 * - To add a new pin constraint to the selected particle(s), click on "Add Pin Constraint".
	 * 
	 */
	[CustomEditor(typeof(ObiRope)), CanEditMultipleObjects] 
	public class ObiRopeEditor : ObiParticleActorEditor
	{

		public class TearableRopeParticleProperty : ParticleProperty
		{
		  public const int TearResistance = 3;

		  public TearableRopeParticleProperty (int value) : base (value){}
		}

		[MenuItem("Assets/Create/Obi/Obi Rope Section")]
		public static void CreateObiRopeSection ()
		{
			ObiEditorUtils.CreateAsset<ObiRopeSection> ();
		}

		[MenuItem("GameObject/3D Object/Obi/Obi Rope (fully set up)",false,4)]
		static void CreateObiRope()
		{
			GameObject c = new GameObject("Obi Rope");
			Undo.RegisterCreatedObjectUndo(c,"Create Obi Rope");
			ObiRope rope = c.AddComponent<ObiRope>();
			ObiCatmullRomCurve path = c.AddComponent<ObiCatmullRomCurve>();
			ObiSolver solver = c.AddComponent<ObiSolver>();
			
			rope.Solver = solver;
			rope.Section = Resources.Load<ObiRopeSection>("DefaultRopeSection");
			rope.ropePath = path;
		}
		
		ObiRope rope;
		SerializedProperty chainLinks;
		
		public override void OnEnable(){
			base.OnEnable();
			rope = (ObiRope)target;
			chainLinks = serializedObject.FindProperty("chainLinks");

			particlePropertyNames.AddRange(new string[]{"Tear Resistance"});
		}
		
		public override void OnDisable(){
			base.OnDisable();
			EditorUtility.ClearProgressBar();
		}

		public override void UpdateParticleEditorInformation(){
			
			for(int i = 0; i < rope.positions.Length; i++)
			{
				wsPositions[i] = rope.GetParticlePosition(i);
				facingCamera[i] = true;		
			}

		}
		
		protected override void SetPropertyValue(ParticleProperty property,int index, float value){
			if (index >= 0 && index < rope.invMasses.Length){
				switch(property){
					case ParticleProperty.Mass: 
							rope.invMasses[index] = 1.0f / Mathf.Max(value,0.00001f);
						break; 
					case ParticleProperty.Radius:
							rope.solidRadii[index] = value;
						break;
					case ParticleProperty.Layer:
							rope.phases[index] = Oni.MakePhase((int)value,rope.SelfCollisions?Oni.ParticlePhase.SelfCollide:0);;
						break;
					case TearableRopeParticleProperty.TearResistance:
							rope.tearResistance[index] = value;
						break;
				}
			}
		}
		
		protected override float GetPropertyValue(ParticleProperty property, int index){
			if (index >= 0 && index < rope.invMasses.Length){
				switch(property){
					case ParticleProperty.Mass:
						return 1.0f/rope.invMasses[index];
					case ParticleProperty.Radius:
						return rope.solidRadii[index];
					case ParticleProperty.Layer:
						return Oni.GetGroupFromPhase(rope.phases[index]);
					case TearableRopeParticleProperty.TearResistance:
						return rope.tearResistance[index];
				}
			}
			return 0;
		}

		public override void OnInspectorGUI() {
			
			serializedObject.Update();

			GUI.enabled = rope.Initialized;
			EditorGUI.BeginChangeCheck();
			editMode = GUILayout.Toggle(editMode,new GUIContent("Edit particles",Resources.Load<Texture2D>("EditParticles")),"LargeButton");
			if (EditorGUI.EndChangeCheck()){
				SceneView.RepaintAll();
			}
			GUI.enabled = true;			

			EditorGUILayout.LabelField("Status: "+ (rope.Initialized ? "Initialized":"Not initialized"));

			GUI.enabled = (rope.ropePath != null && rope.Section != null);
			if (GUILayout.Button("Initialize")){
				if (!rope.Initialized){
					CoroutineJob job = new CoroutineJob();
					routine = EditorCoroutine.StartCoroutine(job.Start(rope.GeneratePhysicRepresentationForMesh()));
					EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
				}else{
					if (EditorUtility.DisplayDialog("Actor initialization","Are you sure you want to re-initialize this actor?","Ok","Cancel")){
						CoroutineJob job = new CoroutineJob();
						routine = EditorCoroutine.StartCoroutine(job.Start(rope.GeneratePhysicRepresentationForMesh()));
						EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
					}
				}
			}
			GUI.enabled = true;

			GUI.enabled = rope.Initialized;
			if (GUILayout.Button("Set Rest State")){
				Undo.RecordObject(rope, "Set rest state");
				rope.PullDataFromSolver(ParticleData.POSITIONS | ParticleData.VELOCITIES);
			}
			GUI.enabled = true;	
			
			if (rope.ropePath == null){
				EditorGUILayout.HelpBox("Rope path spline is missing.",MessageType.Info);
			}
			if (rope.Section == null){
				EditorGUILayout.HelpBox("Rope section is missing.",MessageType.Info);
			}

			EditorGUI.BeginChangeCheck();
			ObiSolver solver = EditorGUILayout.ObjectField("Solver",rope.Solver, typeof(ObiSolver), true) as ObiSolver;
			if (EditorGUI.EndChangeCheck()){
				Undo.RecordObject(rope, "Set solver");
				rope.Solver = solver;
			}

			EditorGUI.BeginChangeCheck();
			ObiCollisionMaterial material = EditorGUILayout.ObjectField("Collision Material",rope.CollisionMaterial, typeof(ObiCollisionMaterial), false) as ObiCollisionMaterial;
			if (EditorGUI.EndChangeCheck()){
				Undo.RecordObject(rope, "Set collision material");
				rope.CollisionMaterial = material;
			}

			bool newSelfCollisions = EditorGUILayout.Toggle(new GUIContent("Self collisions","Enabling this allows particles generated by this actor to interact with each other."),rope.SelfCollisions);
			if (rope.SelfCollisions != newSelfCollisions){
				Undo.RecordObject(rope, "Set self collisions");
				rope.SelfCollisions = newSelfCollisions;
			}

			Editor.DrawPropertiesExcluding(serializedObject,"m_Script","chainLinks");

			bool newThicknessFromParticles = EditorGUILayout.Toggle(new GUIContent("Thickness from particles","Enabling this will allow particle radius to influence rope thickness. Use it for variable-thickness ropes."),rope.ThicknessFromParticles);
			if (rope.ThicknessFromParticles != newThicknessFromParticles){
				Undo.RecordObject(rope, "Set thickness from particles");
				rope.ThicknessFromParticles = newThicknessFromParticles;
			}

			float newTwist = EditorGUILayout.FloatField(new GUIContent("Section twist","Amount of twist applied to each section, in degrees."),rope.SectionTwist);
			if (rope.SectionTwist != newTwist){
				Undo.RecordObject(rope, "Set section twist");
				rope.SectionTwist = newTwist;
			}
			
			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Rendering", EditorStyles.boldLabel);

			ObiRope.RenderingMode newRenderMode = (ObiRope.RenderingMode) EditorGUILayout.EnumPopup(rope.RenderMode);
			if (rope.RenderMode != newRenderMode){
				Undo.RecordObject(rope, "Set rope render mode");
				rope.RenderMode = newRenderMode;
			}

			float newUVAnchor = EditorGUILayout.Slider(new GUIContent("UV anchor","Normalized point along the rope where the V texture coordinate starts. Useful when changing rope length."),rope.UVAnchor,0,1);
			if (rope.UVAnchor != newUVAnchor){
				Undo.RecordObject(rope, "Set rope uv anchor");
				rope.UVAnchor = newUVAnchor;
			}

			// Render-mode specific stuff:
			if (rope.RenderMode != ObiRope.RenderingMode.Chain)
			{
				ObiRopeSection newSection = EditorGUILayout.ObjectField(new GUIContent("Section","Section asset to be extruded along the rope path.")
																	,rope.Section, typeof(ObiRopeSection), false) as ObiRopeSection;
				if (rope.Section != newSection){
					Undo.RecordObject(rope, "Set rope section");
					rope.Section = newSection;
				}
	
				float newThickness = EditorGUILayout.FloatField(new GUIContent("Section thickness scale","Scales mesh thickness."),rope.SectionThicknessScale);
				if (rope.SectionThicknessScale != newThickness){
					Undo.RecordObject(rope, "Set rope section thickness");
					rope.SectionThicknessScale = newThickness;
				}

				uint newSmoothness = (uint)EditorGUILayout.IntSlider(new GUIContent("Smoothness","Level of smoothing applied to the rope path."),Convert.ToInt32(rope.Smoothing),0,3);
				if (rope.Smoothing != newSmoothness){
					Undo.RecordObject(rope, "Set smoothness");
					rope.Smoothing = newSmoothness;
				}

				Vector2 newUVScale = EditorGUILayout.Vector2Field(new GUIContent("UV scale","Scaling of the uv coordinates generated for the rope. The u coordinate wraps around the whole rope section, and the v spans the full length of the rope."),rope.UVScale);
				if (rope.UVScale != newUVScale){
					Undo.RecordObject(rope, "Set rope uv scale");
					rope.UVScale = newUVScale;
				}

				bool newNormalizeV = EditorGUILayout.Toggle(new GUIContent("Normalize V","Scaling of the uv coordinates generated for the rope. The u coordinate wraps around the whole rope section, and the v spans the full length of the rope."),rope.NormalizeV);
				if (rope.NormalizeV != newNormalizeV){
					Undo.RecordObject(rope, "Set normalize v");
					rope.NormalizeV = newNormalizeV;
				}

			}else{

				Vector3 newLinkScale = EditorGUILayout.Vector3Field(new GUIContent("Link scale","Scale applied to each chain link."),rope.LinkScale);
				if (rope.LinkScale != newLinkScale){
					Undo.RecordObject(rope, "Set chain link scale");
					rope.LinkScale = newLinkScale;
				}

				bool newRandomizeLinks = EditorGUILayout.Toggle(new GUIContent("Randomize links","Toggling this on this causes each chain link to be selected at random from the set of provided links."),rope.RandomizeLinks);
				if (rope.RandomizeLinks != newRandomizeLinks){
					Undo.RecordObject(rope, "Set randomize links");
					rope.RandomizeLinks = newRandomizeLinks;
				}

				EditorGUI.BeginChangeCheck();
				EditorGUILayout.PropertyField(chainLinks, true);	
				if (EditorGUI.EndChangeCheck()){
					// update the chain representation in response to a change in available link templates:
					serializedObject.ApplyModifiedProperties();	
					rope.GenerateProceduralChainLinks();
				}
			}

			// Progress bar:
			EditorCoroutine.ShowCoroutineProgressBar("Generating physical representation...",routine);
			
			// Apply changes to the serializedProperty
			if (GUI.changed){
				serializedObject.ApplyModifiedProperties();
			}
			
		}
		
	}
}


