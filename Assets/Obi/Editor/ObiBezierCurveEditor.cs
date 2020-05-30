using UnityEditor;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Obi{
	
	/**
	 * Custom inspector for ObiSpline component. 
	 */
	
	[CustomEditor(typeof(ObiBezierCurve))] 
	public class ObiBezierCurveEditor : Editor
	{
		
		ObiBezierCurve spline;

		private static int curvePreviewResolution = 10;
		private bool hideSplineHandle;

		private bool[] selectedStatus;
		private Vector3[] handleVectors;
		private Vector3 handleScale = Vector3.one;
		private Quaternion handleRotation = Quaternion.identity;

		Rect uirect;
		
		public void OnEnable(){
			spline = (ObiBezierCurve)target;
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

			ResizeCPArrays();

			Editor.DrawPropertiesExcluding(serializedObject,"m_Script");
			
			EditorGUI.BeginChangeCheck();
			bool closed = EditorGUILayout.Toggle("Closed",spline.Closed);
			if (EditorGUI.EndChangeCheck()){
				Undo.RecordObject(spline, "Open/Close curve");
				spline.Closed = closed;
			}

			//-------------------------------
			// Visualization options
			//-------------------------------
			hideSplineHandle = EditorGUILayout.Toggle("Hide spline handle",hideSplineHandle);

			EditorGUI.showMixedValue = false;
			ObiBezierCurve.BezierCPMode mode = ObiBezierCurve.BezierCPMode.Free;
			bool firstSelected = true;
			for (int i = 0; i < spline.controlPoints.Count; ++i){
				if (selectedStatus[i]){
					if (firstSelected){
						mode = spline.GetControlPointMode(i);
						firstSelected = false;
					}else if (mode != spline.GetControlPointMode(i)){
						EditorGUI.showMixedValue = true;
						break;
					}
				}
			}

			EditorGUI.BeginChangeCheck();
			ObiBezierCurve.BezierCPMode newMode = (ObiBezierCurve.BezierCPMode) EditorGUILayout.EnumPopup("Handle mode",mode,GUI.skin.FindStyle("DropDown"));
			EditorGUI.showMixedValue = false;
			if (EditorGUI.EndChangeCheck()){

				Undo.RecordObject(spline, "Change control points mode");

				for (int i = 0; i < spline.controlPoints.Count; ++i){
					if (selectedStatus[i]){
						spline.SetControlPointMode(i,newMode);
					}
				}
			}

			if (GUILayout.Button("Add span")){
				Undo.RecordObject(spline, "Add span");
				spline.AddSpan();
			}
			
			if (GUILayout.Button("Remove control point")){

				Undo.RecordObject(spline, "Remove control point");

				for (int i = 0; i < spline.controlPoints.Count; ++i){
					if (selectedStatus[i]){ 
						spline.RemoveCurvePoint((i + 1) / 3);
						break;
					}
				}

				for (int i = 0; i < selectedStatus.Length; ++i)
					selectedStatus[i] = false;

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

					// Dont consider cp handles for avg in rotation mode.
					if (Tools.current == Tool.Rotate && i%3 != 0) 
						continue;

					averagePos += controlPoints[i];
					numSelectedCPs++;
				}
			}
			averagePos /= numSelectedCPs;

			// Calculate handle rotation, for local or world pivot modes.
			Quaternion pivotRotation = Tools.pivotRotation == PivotRotation.Local ? spline.transform.rotation : Quaternion.identity;
			Tools.hidden = hideSplineHandle;
	
			int oldHotControl = GUIUtility.hotControl;

			// Transform handles:	
			if (numSelectedCPs > 0){

				switch (Tools.current)
				{
					case Tool.Move:{
						EditorGUI.BeginChangeCheck();
						Vector3 newPos = Handles.PositionHandle(averagePos,pivotRotation);
						if (EditorGUI.EndChangeCheck()){
							Undo.RecordObject(spline, "Move control point");
		
							Vector3 delta = spline.transform.InverseTransformVector(newPos - averagePos);
		
							for (int i = 0; i < controlPoints.Length; ++i){

								if (selectedStatus[i]){
									if (!spline.IsHandle(i))
										spline.DisplaceControlPoint(i,delta);
									else{
										int cp = spline.GetHandleControlPointIndex(i);
										if (!selectedStatus[cp])
											spline.DisplaceControlPoint(i,delta);
									}
								}
							}
						}
					}break;

					case Tool.Scale:{
						EditorGUI.BeginChangeCheck();
						handleScale = Handles.ScaleHandle(handleScale,averagePos,pivotRotation,HandleUtility.GetHandleSize(averagePos));

						// handle has just been (de)selected:
						if (GUIUtility.hotControl != oldHotControl){

							handleScale = Vector3.one;

							if (Tools.pivotMode == PivotMode.Center){
								for (int i = 0; i < controlPoints.Length; ++i){
									if (selectedStatus[i]){
										handleVectors[i] = controlPoints[i] - averagePos;
									}
								}
							}else{
								for (int i = 0; i < controlPoints.Length; ++i){
									handleVectors[i] = controlPoints[i] - controlPoints[spline.GetHandleControlPointIndex(i)];
								}
							}
						}

						if (EditorGUI.EndChangeCheck()){
							Undo.RecordObject(spline, "Scale control point");

							if (Tools.pivotMode == PivotMode.Center){
								for (int i = 0; i < controlPoints.Length; ++i){
									if (selectedStatus[i]){
										Vector3 newPos = averagePos + Vector3.Scale(handleVectors[i],handleScale);
										Vector3 delta = spline.transform.InverseTransformVector(newPos - controlPoints[i]);
										spline.DisplaceControlPoint(i,delta);
									}
								}
							}else{
								// Scale all handles of selected control points relative to their control point:
								for (int i = 0; i < controlPoints.Length; ++i){
									if (selectedStatus[i]){
										List<int> handles = spline.GetHandleIndicesForControlPoint(i);
										foreach (int h in handles){
											Vector3 newPos = controlPoints[i] + Vector3.Scale(handleVectors[h],handleScale);
											Vector3 delta = spline.transform.InverseTransformVector(newPos - controlPoints[h]);
											spline.DisplaceControlPoint(h,delta);
										}

									}
								}
							}
						}
					}break;

					case Tool.Rotate:{
						EditorGUI.BeginChangeCheck();
						handleRotation = Handles.RotationHandle(handleRotation,averagePos);

						// handle has just been (de)selected:
						if (GUIUtility.hotControl != oldHotControl){

							handleRotation = Quaternion.identity;

							if (Tools.pivotMode == PivotMode.Center){
								for (int i = 0; i < controlPoints.Length; ++i){
									if (!spline.IsHandle(i) && selectedStatus[i]){
										handleVectors[i] = controlPoints[i] - averagePos;
									}
								}
							}else{
								for (int i = 0; i < controlPoints.Length; ++i){
									handleVectors[i] = controlPoints[i] - controlPoints[spline.GetHandleControlPointIndex(i)];
								}
							}
						}

						if (EditorGUI.EndChangeCheck()){
							Undo.RecordObject(spline, "Rotate control point");

							if (Tools.pivotMode == PivotMode.Center){

								// Rotate all selected control points around their average:
								for (int i = 0; i < controlPoints.Length; ++i){
									if (!spline.IsHandle(i) && selectedStatus[i]){
										Vector3 newPos = averagePos + handleRotation*handleVectors[i];
										Vector3 delta = spline.transform.InverseTransformVector(newPos - controlPoints[i]);
										spline.DisplaceControlPoint(i,delta);
									}
								}
							}else{

								// Rotate all handles of selected control points around their control point:
								for (int i = 0; i < controlPoints.Length; ++i){
									if (selectedStatus[i]){
										List<int> handles = spline.GetHandleIndicesForControlPoint(i);
										foreach (int h in handles){
											Vector3 newPos = controlPoints[i] + handleRotation*handleVectors[h];
											Vector3 delta = spline.transform.InverseTransformVector(newPos - controlPoints[h]);
											spline.DisplaceControlPoint(h,delta);
										}

									}
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
				Handles.color = Color.blue;
				for (int i = 0; i < controlPoints.Length; i+=3){

					int prev = Mathf.Max(0,i-1);
					int next = Mathf.Min(i+1,controlPoints.Length-1);

					Handles.DrawDottedLine(spline.controlPoints[prev],spline.controlPoints[i],2);
					Handles.DrawDottedLine(spline.controlPoints[i],spline.controlPoints[next],2);
				}

				Handles.matrix = prevMatrix; 

				// Draw control points:
				for (int i = 0; i < controlPoints.Length; ++i){
	
					Handles.color = i%3 == 0 ? Color.white : Color.blue;

					if (spline.Closed && (i == 0 || i == controlPoints.Length-1)){
						if (selectedStatus[0] || selectedStatus[controlPoints.Length-1])
							Handles.color = Color.red;
					}else if (selectedStatus[i]){
						Handles.color = Color.red;
					}
	
					float size = HandleUtility.GetHandleSize(controlPoints[i])*0.1f;
					if (i%3 == 0)
						Handles.SphereHandleCap(0,controlPoints[i],Quaternion.identity,size,EventType.Repaint);
					else
						Handles.DotHandleCap(0,controlPoints[i],Quaternion.identity,size*0.25f,EventType.Repaint);
				}

			}	

			// Control point selection handle:
			if (ObiSplineHandles.SplineCPSelector(controlPoints,selectedStatus))
				Repaint();

			// Draw cp tool handles:
			SplineCPTools(controlPoints);
		
		}

		[DrawGizmo(GizmoType.Selected)]
	    private static void GizmoTest(ObiBezierCurve spline, GizmoType gizmoType)
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

