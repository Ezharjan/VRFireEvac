using UnityEditor;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Obi{
	
	/**
	 * Custom inspector for ObiRopeExtruder component. 
	 */
	
	[CustomEditor(typeof(ObiRopeCursor)), CanEditMultipleObjects] 
	public class ObiRopeCursorEditor : Editor
	{
		
		ObiRopeCursor cursor;
		
		public void OnEnable(){
			cursor = (ObiRopeCursor)target;
		}
		
		public override void OnInspectorGUI() {
			
			serializedObject.UpdateIfRequiredOrScript();
			
			Editor.DrawPropertiesExcluding(serializedObject,"m_Script");
			
			// Apply changes to the serializedProperty
			if (GUI.changed){
				serializedObject.ApplyModifiedProperties();
			}
			
		}

		public void OnSceneGUI(){

			if (Event.current.type == EventType.Repaint){

				ObiRope rope = cursor.rope;

				if (rope == null) 
					return;

				ObiDistanceConstraintBatch distanceBatch = rope.DistanceConstraints.GetBatches()[0] as ObiDistanceConstraintBatch;
	
				Handles.color = Color.yellow;
				int constraint = rope.GetConstraintIndexAtNormalizedCoordinate(cursor.normalizedCoord);
				Vector3 pos1 = rope.GetParticlePosition(distanceBatch.springIndices[constraint*2]);
				Vector3 pos2 = rope.GetParticlePosition(distanceBatch.springIndices[constraint*2+1]);

				if (cursor.direction){
					Handles.DrawWireDisc(pos1,pos2-pos1,rope.thickness*2);
					Vector3 direction = pos2-pos1;
					if (direction != Vector3.zero)	
						Handles.ArrowHandleCap(0,pos1,Quaternion.LookRotation(direction),0.2f,EventType.Repaint);
				}else{
					Handles.DrawWireDisc(pos2,pos1-pos2,rope.thickness*2);	
					Vector3 direction = pos1-pos2;
					if (direction != Vector3.zero)	
						Handles.ArrowHandleCap(0,pos2,Quaternion.LookRotation(direction),0.2f,EventType.Repaint);
				}
			}

		}

		
	}
}

