using UnityEditor;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Obi{
	
	/**
	 * Custom inspector for ObiSpline component. 
	 */
	
	[CustomEditor(typeof(ObiCatmullRomCurve))] 
	public class ObiCatmullRomCurveEditor : Editor
	{
		
		ObiCatmullRomCurve spline;

		private static int curvePreviewResolution = 10;
		private bool hideSplineHandle;

		private bool[] selectedStatus;
		private Vector3[] handleVectors;
		Vector3 scale = Vector3.one;

		Rect uirect;
		
		public void OnEnable(){
			spline = (ObiCatmullRomCurve)target;
			hideSplineHandle = false;
			selectedStatus = new bool[spline.controlPoints.Count];
			handleVectors = new Vector3[spline.controlPoints.Count];
		}

		private void ResizeCPArrays(){	
			Array.Resize(ref selectedStatus,spline.controlPoints.Count);
			Array.Resize(ref handleVectors,spline.controlPoints.Count);
		}
		
		public override void OnInspectorGUI() {
			
			serializedObject.UpdateIfRequiredOrScript();

			Editor.DrawPropertiesExcluding(serializedObject,"m_Script");
			
			EditorGUI.BeginChangeCheck();
			bool closed = EditorGUILayout.Toggle("Closed",spline.Closed);
			if (EditorGUI.EndChangeCheck()){
				Undo.RecordObject(spline, "Open/Close curve");
					spline.Closed = closed;
			}

			hideSplineHandle = EditorGUILayout.Toggle("Hide spline handle",hideSplineHandle);

			if (GUILayout.Button("Add control point")){
				Undo.RecordObject(spline, "Add control point");

				for (int i = 0; i < spline.controlPoints.Count; ++i){
					if (selectedStatus[i] || i == spline.controlPoints.Count-1){	

						Vector3 cp = spline.GetPositionAt((i-1+0.5f)/(float)(spline.controlPoints.Count-3));

						spline.controlPoints.Insert(i+1,cp);
						break;
					}
				}

			}
			
			if (GUILayout.Button("Remove selected control points")){

				Undo.RecordObject(spline, "Remove control points");
				List<int> toBeDeleted = new List<int>();

				for (int i = 0; i < spline.controlPoints.Count; ++i){
					if (selectedStatus[i]){ 
						toBeDeleted.Add(i);	
						selectedStatus[i] = false;
					}
				}

				if (spline.controlPoints.Count - toBeDeleted.Count < 4)
					EditorUtility.DisplayDialog("Ooops!","Cannot remove that many points. Catmull-Rom splines need at least 4 points to be defined.","Ok");
				else{	
					toBeDeleted.Sort();
					toBeDeleted.Reverse();
					foreach(int i in toBeDeleted)
						spline.controlPoints.RemoveAt(i);
				}

			}
			
			// Apply changes to the serializedProperty
			if (GUI.changed){
				serializedObject.ApplyModifiedProperties();
			}
			
		}


		public void SplineCPTools(Vector3[] controlPoints){

			// Find center of all selected control points:
			Vector3 averagePos = Vector3.zero;
			int numSelectedCPs = 0;
			for (int i = 0; i < controlPoints.Length; ++i){
				if (selectedStatus[i]){
					averagePos += controlPoints[i];
					numSelectedCPs++;
				}
			}
			averagePos /= numSelectedCPs;

			// Calculate handle rotation, for local or world pivot modes.
			Quaternion handleRotation = Tools.pivotRotation == PivotRotation.Local ? spline.transform.rotation : Quaternion.identity;
			Tools.hidden = hideSplineHandle;
	
			int oldHotControl = GUIUtility.hotControl;

			// Transform handles:	
			if (numSelectedCPs > 0){

				switch (Tools.current)
				{
					case Tool.Move:{
						EditorGUI.BeginChangeCheck();
						Vector3 newPos = Handles.PositionHandle(averagePos,handleRotation);
						if (EditorGUI.EndChangeCheck()){
							Undo.RecordObject(spline, "Move control point");
		
							Vector3 delta = spline.transform.InverseTransformVector(newPos - averagePos);
		
							for (int i = 0; i < controlPoints.Length; ++i){
								if (selectedStatus[i]){
									spline.DisplaceControlPoint(i,delta);
								}
							}
		
						}
					}break;

					case Tool.Scale:{
						EditorGUI.BeginChangeCheck();
						scale = Handles.ScaleHandle(scale,averagePos,handleRotation,HandleUtility.GetHandleSize(averagePos));

						// handle has just been (de)selected:
						if (GUIUtility.hotControl != oldHotControl){
							scale = Vector3.one;
							for (int i = 0; i < controlPoints.Length; ++i){
								if (selectedStatus[i]){
									handleVectors[i] = controlPoints[i] - averagePos;
								}
							}
						}

						if (EditorGUI.EndChangeCheck()){

							Undo.RecordObject(spline, "Scale control point");
							for (int i = 0; i < controlPoints.Length; ++i){
								if (selectedStatus[i]){
									Vector3 newPos = averagePos + Vector3.Scale(handleVectors[i],scale);
									Vector3 delta = spline.transform.InverseTransformVector(newPos - controlPoints[i]);
									spline.DisplaceControlPoint(i,delta);
								}
							}
		
						}
					}break;

					case Tool.Rotate:{
						EditorGUI.BeginChangeCheck();
						Quaternion newRotation = Handles.RotationHandle(Quaternion.identity,averagePos);

						// handle has just been (de)selected:
						if (GUIUtility.hotControl != oldHotControl){
							for (int i = 0; i < controlPoints.Length; ++i){
								if (selectedStatus[i]){
									handleVectors[i] = controlPoints[i] - averagePos;
								}
							}
						}

						if (EditorGUI.EndChangeCheck()){
							Undo.RecordObject(spline, "Rotate control point");

							for (int i = 0; i < controlPoints.Length; ++i){
								if (selectedStatus[i]){
									Vector3 newPos = averagePos + newRotation*handleVectors[i];
									Vector3 delta = spline.transform.InverseTransformVector(newPos - controlPoints[i]);
									spline.DisplaceControlPoint(i,delta);
								}
							}
						}
					}break;
				}
			}
		}

		/**
		 * Draws selected pin constraints in the scene view.
		 */
		public void OnSceneGUI(){

			ResizeCPArrays();

			if (spline.controlPoints.Count < 4)
				return;

			// World space control points:
			Vector3[] controlPoints = new Vector3[spline.controlPoints.Count];
			for (int i = 0; i < controlPoints.Length; ++i)
				controlPoints[i] = spline.transform.TransformPoint(spline.controlPoints[i]);

			if (Event.current.type == EventType.Repaint){

				Matrix4x4 prevMatrix = Handles.matrix;
				Handles.color = Color.white;
				Handles.matrix = spline.transform.localToWorldMatrix;

				// Draw tangents:
				if (!spline.Closed){
					Handles.color = Color.blue;
					Handles.DrawDottedLine(spline.controlPoints[1],spline.controlPoints[0],2);
					Handles.DrawDottedLine(spline.controlPoints[spline.controlPoints.Count-2],
										   spline.controlPoints[spline.controlPoints.Count-1],2);
				}

				Handles.matrix = prevMatrix; 

				// Draw control points:
				for (int i = 0; i < controlPoints.Length; ++i){
	
					Handles.color = (!spline.Closed && (i == 0 || i == controlPoints.Length-1)) ? Color.blue : Color.white;

					if (spline.Closed && (i <= 2 || i >= controlPoints.Length-3)){
						if ((i == 1 || i == controlPoints.Length-2) && (selectedStatus[1] || selectedStatus[controlPoints.Length-2]))
							Handles.color = Color.red;
						else if ((i == 2 || i == controlPoints.Length-1) && (selectedStatus[2] || selectedStatus[controlPoints.Length-1]))
							Handles.color = Color.red;
						else if ((i == 0 || i == controlPoints.Length-3) && (selectedStatus[0] || selectedStatus[controlPoints.Length-3]))
							Handles.color = Color.red;
					}else if (selectedStatus[i]){
						Handles.color = Color.red;
					}
	
					float size = HandleUtility.GetHandleSize(controlPoints[i])*0.1f;
					if (!spline.Closed && (i == 0 || i == controlPoints.Length-1))
						Handles.DotHandleCap(0,controlPoints[i],Quaternion.identity,size*0.25f,EventType.Repaint);
					else
						Handles.SphereHandleCap(0,controlPoints[i],Quaternion.identity,size,EventType.Repaint);
		
				}

			}	

			// Control point selection handle:
			if (ObiSplineHandles.SplineCPSelector(controlPoints,selectedStatus))
				Repaint();

			// Draw cp tool handles:
			SplineCPTools(controlPoints);
		
		}


		[DrawGizmo(GizmoType.Selected)]
	    private static void GizmoTest(ObiCatmullRomCurve spline, GizmoType gizmoType)
	    {

			Matrix4x4 prevMatrix = Handles.matrix;
			Color oldColor = Handles.color;

	        // Draw the curve:
			int curveSegments = spline.GetNumSpans() * curvePreviewResolution;
			Vector3[] samples = new Vector3[curveSegments+1];
			for (int i = 0; i <= curveSegments; ++i){
				samples[i] = spline.GetPositionAt(i/(float)curveSegments);
			}

			Handles.matrix = spline.transform.localToWorldMatrix;
			Handles.color = Color.white;
			Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
			Handles.DrawPolyLine(samples);

			Handles.color = new Color(1,1,1,0.25f);
			Handles.zTest = UnityEngine.Rendering.CompareFunction.Greater;
			Handles.DrawPolyLine(samples);

			Handles.color = oldColor;
			Handles.matrix = prevMatrix; 
	    }
		
	}
}

