using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Obi{


/**
 * Catmull-rom spline. Provides no tangent handles at the control points.
 */
[ExecuteInEditMode]
public class ObiCatmullRomCurve : ObiCurve {

	[HideInInspector] public Vector3 lastOpenCP0;
	[HideInInspector] public Vector3 lastOpenCP1;
	[HideInInspector] public Vector3 lastOpenCPN;

	public override void Awake(){
		minPoints = 4;
		unusedPoints = 2;
		base.Awake();
	}

	protected override void SetClosed(bool closed){

		if (this.closed == closed) return;

		if (!this.closed && closed){

			lastOpenCP0 = controlPoints[0];
			lastOpenCP1 = controlPoints[1];
			lastOpenCPN = controlPoints[controlPoints.Count-1];	

			controlPoints[0] = controlPoints[controlPoints.Count-3];
			controlPoints[1] = controlPoints[controlPoints.Count-2];
			controlPoints[controlPoints.Count-1] = controlPoints[2];
		}else{
			controlPoints[0] = lastOpenCP0;
			controlPoints[1] = lastOpenCP1;
			controlPoints[controlPoints.Count-1] = lastOpenCPN;
		}

		this.closed = closed;
	}

	public override void DisplaceControlPoint(int index, Vector3 delta){

		if (index < 0 || index >= controlPoints.Count) return;

		if (closed){
			if (index == 0 || index == controlPoints.Count-3){
				controlPoints[0] += delta;
				controlPoints[controlPoints.Count-3] += delta;
			}else if (index == 1 || index == controlPoints.Count-2){
				controlPoints[1] += delta; 
				controlPoints[controlPoints.Count-2] += delta;
			}else if (index == 2 || index == controlPoints.Count-1){
				controlPoints[2] += delta; 
				controlPoints[controlPoints.Count-1] += delta;
			}else{
				controlPoints[index] += delta;
			}
		}else{
			controlPoints[index] += delta;
		}

	}

	public override int GetNumSpans(){
		
		return controlPoints.Count-unusedPoints-1;

	}

	public override int GetSpanControlPointForMu(float mu, out float spanMu){

		int spanCount = GetNumSpans();
		spanMu = mu * spanCount;
		int i = (mu >= 1f) ? (spanCount - 1) : (int) spanMu;
		spanMu -= i;

		return i;
	}
	
	/**
	* 1D catmull rom spline interpolation
	*/
	protected override float Evaluate1D(float y0, float y1, float y2, float y3, float mu){
		
		float a0,a1,a2,a3,mu2;
    	mu2 = mu*mu;
    
    	a0 = -0.5f*y0 + 1.5f*y1 - 1.5f*y2 + 0.5f*y3;
    	a1 = y0 - 2.5f*y1 + 2f*y2 - 0.5f*y3;
    	a2 = -0.5f*y0 + 0.5f*y2;
    	a3 = y1;
    
    	return(a0*mu*mu2+a1*mu2+a2*mu+a3);
		
	}

	/**
	* 1D catmull rom spline second derivative
	*/
	protected override float EvaluateFirstDerivative1D(float y0, float y1, float y2, float y3, float mu){
		
		float a0,a1,a2,mu2;
		mu2 = mu*mu;
		
		a0 = -0.5f*y0 + 1.5f*y1 - 1.5f*y2 + 0.5f*y3;
		a1 = y0 - 2.5f*y1 + 2f*y2 - 0.5f*y3;
		a2 = -0.5f*y0 + 0.5f*y2;

		return(3*a0*mu2 + 2*a1*mu + a2);
	}

	
	/**
	* 1D catmull rom spline second derivative
	*/
	protected override float EvaluateSecondDerivative1D(float y0, float y1, float y2, float y3, float mu){
		
		float a0,a1;
		
		a0 = -0.5f*y0 + 1.5f*y1 - 1.5f*y2 + 0.5f*y3;
		a1 = y0 - 2.5f*y1 + 2f*y2 - 0.5f*y3;
		
		return(6*a0*mu + 2*a1 );
		
	}
	

}
}
