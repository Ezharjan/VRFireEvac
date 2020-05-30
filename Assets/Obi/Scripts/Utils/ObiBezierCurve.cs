using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/**
 * Obi spline class.
 */

namespace Obi{


/**
 * Bèzier spline. Provides tangent handles at the control points, which offers greater control compared to
 * Catmull-rom splines.
 */
[ExecuteInEditMode]
public class ObiBezierCurve : ObiCurve {


	public enum BezierCPMode {
		Free,
		Aligned,
		Mirrored
	}

	[HideInInspector] public List<BezierCPMode> controlPointModes = null;	
	[HideInInspector] public BezierCPMode lastOpenCPMode;
	[HideInInspector] public Vector3 lastOpenCP;

	public override void Awake(){
		minPoints = 4;
		unusedPoints = 2;
		pointStride = 3;
		base.Awake();
		if (controlPointModes == null){
			controlPointModes = new List<BezierCPMode>(){BezierCPMode.Free,BezierCPMode.Free};
		}
	}

	protected override void SetClosed(bool closed){

		if (this.closed == closed) return;

		if (!this.closed && closed){
			lastOpenCP = controlPoints[0];
			lastOpenCPMode = controlPointModes[0];
			controlPoints[0] = controlPoints[controlPoints.Count-1];
			controlPointModes[0] = controlPointModes[controlPointModes.Count-1];
		}else{
			controlPoints[0] = lastOpenCP;
			controlPointModes[0] = lastOpenCPMode;
		}

		this.closed = closed;
		EnforceMode(0);
	}

	public override int GetNumSpans(){
		
		return ( controlPoints.Count + ((controlPoints.Count-4)/3) ) / 4;

	}

	public bool IsHandle(int index){
		return index % 3 != 0; 
	}

	public int GetHandleControlPointIndex(int index){

		if (index < 0 || index >= controlPoints.Count) 
			return -1;

		if (index % 3 == 1)
			return index-1;
		else if (index % 3 == 2)
				return index+1;
		else return index;

	}

	public List<int> GetHandleIndicesForControlPoint(int index){

		List<int> handleIndices = new List<int>();
		if (index < 0 || index >= controlPoints.Count) return handleIndices;

		if (!IsHandle(index)) {

			if (closed) {
				if (index == 0) {
					handleIndices.Add(1);
					handleIndices.Add(controlPoints.Count - 2);
				}
				else if (index == controlPoints.Count - 1) {
					handleIndices.Add(1);
					handleIndices.Add(index - 1);
				}
				else {
					handleIndices.Add(index + 1);
					handleIndices.Add(index - 1);
				}
			}
			else {
				if (index > 0) {
					handleIndices.Add(index - 1);
				}
				if (index + 1 < controlPoints.Count) {
					handleIndices.Add(index + 1);
				}
			}

		}

		return handleIndices;

	}

	public override void DisplaceControlPoint(int index, Vector3 delta){

		if (index < 0 || index >= controlPoints.Count) return;

		if (!IsHandle(index)) {

			if (closed) {
				if (index == 0) {
					controlPoints[1] += delta;
					controlPoints[controlPoints.Count - 2] += delta;
					controlPoints[controlPoints.Count - 1] += delta;
				}
				else if (index == controlPoints.Count - 1) {
					controlPoints[0] += delta;
					controlPoints[1] += delta;
					controlPoints[index - 1] += delta;
				}
				else {
					controlPoints[index - 1] += delta;
					controlPoints[index + 1] += delta;
				}
			}
			else {
				if (index > 0) {
					controlPoints[index - 1] += delta;
				}
				if (index + 1 < controlPoints.Count) {
					controlPoints[index + 1] += delta;
				}
			}

		}

		controlPoints[index] += delta;
		EnforceMode(index);

	}

	public override int GetSpanControlPointForMu(float mu, out float spanMu){

		int spanCount = GetNumSpans();
		spanMu = mu * spanCount;
		int i = (mu >= 1f) ? (spanCount - 1) : (int) spanMu;
		spanMu -= i;

		return i * 3;
	}

	public BezierCPMode GetControlPointMode (int index) {
		int i = (index + 1) / 3;
		return controlPointModes[i];
	}

	public void SetControlPointMode (int index, BezierCPMode mode) {
		int i = (index + 1) / 3;
		controlPointModes[i] = mode;

		if (closed) {
			if (i == 0) {
				controlPointModes[controlPointModes.Count - 1] = mode;
			}
			else if (i == controlPointModes.Count - 1) {
				controlPointModes[0] = mode;
			}
		}

		EnforceMode(index);
	}

	public void EnforceMode (int index) {
		int modeIndex = (index + 1) / 3;
		BezierCPMode mode = controlPointModes[modeIndex];
		if (mode == BezierCPMode.Free || !closed && (modeIndex == 0 || modeIndex == controlPointModes.Count - 1)) {
			return;
		}

		int middleIndex = modeIndex * 3;
		int fixedIndex, enforcedIndex;
		if (index <= middleIndex) {
			fixedIndex = middleIndex - 1;
			if (fixedIndex < 0) {
				fixedIndex = controlPoints.Count - 2;
			}
			enforcedIndex = middleIndex + 1;
			if (enforcedIndex >= controlPoints.Count) {
				enforcedIndex = 1;
			}
		}
		else {
			fixedIndex = middleIndex + 1;
			if (fixedIndex >= controlPoints.Count) {
				fixedIndex = 1;
			}
			enforcedIndex = middleIndex - 1;
			if (enforcedIndex < 0) {
				enforcedIndex = controlPoints.Count - 2;
			}
		}

		Vector3 middle = controlPoints[middleIndex];
		Vector3 enforcedTangent = middle - controlPoints[fixedIndex];
		if (mode == BezierCPMode.Aligned) {
			enforcedTangent = enforcedTangent.normalized * Vector3.Distance(middle, controlPoints[enforcedIndex]);
		}
		controlPoints[enforcedIndex] = middle + enforcedTangent;
	}

	public void AddSpan(){

		int index = controlPoints.Count-1;

		Vector3 lastPosition = controlPoints[index];

		controlPoints.Add(lastPosition+Vector3.right*0.5f);
		controlPoints.Add(lastPosition+Vector3.right);
		controlPoints.Add(lastPosition+Vector3.right*1.5f);

		controlPointModes.Add(ObiBezierCurve.BezierCPMode.Free);

		EnforceMode(index);

		if (closed) {
			controlPoints[controlPoints.Count - 1] = controlPoints[0];
			controlPointModes[controlPointModes.Count - 1] = controlPointModes[0];
			EnforceMode(0);
		}

	}

	public void RemoveCurvePoint(int curvePoint){

		if (controlPoints.Count <= 4) return;

		int firstPoint = Mathf.Max(0,curvePoint * 3 - 1); 
		int numPoints = 3;

		// First and last spans have 1 point less to remove.
		if (firstPoint == controlPoints.Count-2)
			firstPoint -= 1;

		controlPoints.RemoveRange(firstPoint,numPoints);
		controlPointModes.RemoveAt(curvePoint);

		if (closed){
			if(firstPoint == controlPoints.Count){
				controlPoints[0] = controlPoints[controlPoints.Count-1];
				controlPointModes[0] = controlPointModes[controlPointModes.Count-1];
			}else if (firstPoint == 0){
				controlPoints[controlPoints.Count-1] = controlPoints[0];
				controlPointModes[controlPointModes.Count-1] = controlPointModes[0];
			}
		}

		EnforceMode(firstPoint);

	}
	
	/**
	* 1D bezier spline interpolation
	*/
	protected override float Evaluate1D(float y0, float y1, float y2, float y3, float mu){
		
		float imu = 1 - mu;
		return imu * imu * imu * y0 +
			3f * imu * imu * mu * y1 +
			3f * imu * mu * mu * y2 +
			mu * mu * mu * y3;

		/*float a0,a1,a2,a3,mu2;
    	mu2 = mu*mu;
    
    	a0 = -0.5f*y0 + 1.5f*y1 - 1.5f*y2 + 0.5f*y3;
    	a1 = y0 - 2.5f*y1 + 2f*y2 - 0.5f*y3;
    	a2 = -0.5f*y0 + 0.5f*y2;
    	a3 = y1;
    
    	return(a0*mu*mu2+a1*mu2+a2*mu+a3);*/
		
	}

	/**
	* 1D catmull rom spline second derivative
	*/
	protected override float EvaluateFirstDerivative1D(float y0, float y1, float y2, float y3, float mu){
		
		float imu = 1 - mu;
		return  3f * imu * imu * (y1 - y0) +
				6f * imu * mu * (y2 - y1) +
				3f * mu * mu * (y3 - y2);

		/*float a0,a1,a2,mu2;
		mu2 = mu*mu;
		
		a0 = -0.5f*y0 + 1.5f*y1 - 1.5f*y2 + 0.5f*y3;
		a1 = y0 - 2.5f*y1 + 2f*y2 - 0.5f*y3;
		a2 = -0.5f*y0 + 0.5f*y2;

		return(3*a0*mu2 + 2*a1*mu + a2);*/
	}

	
	/**
	* 1D catmull rom spline second derivative
	*/
	protected override float EvaluateSecondDerivative1D(float y0, float y1, float y2, float y3, float mu){
		
		float imu = 1 - mu;
		return  3f * imu * imu * (y1 - y0) +
				6f * imu * mu * (y2 - y1) +
				3f * mu * mu * (y3 - y2);

		/*float a0,a1,a2;
		
		a0 = -0.5f*y0 + 1.5f*y1 - 1.5f*y2 + 0.5f*y3;
		a1 = y0 - 2.5f*y1 + 2f*y2 - 0.5f*y3;
		
		return(6*a0*mu + 2*a1 );*/
		
	}
	

}
}
