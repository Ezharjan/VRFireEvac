using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/**
 * Obi spline class.
 */

namespace Obi{

[ExecuteInEditMode]
public abstract class ObiCurve : MonoBehaviour {
	
	protected int arcLenghtSamples = 5;
	protected int minPoints = 4;
	protected int unusedPoints = 2;
	protected int pointStride = 1;

	[HideInInspector] public List<Vector3> controlPoints = null;
	[HideInInspector][SerializeField] protected List<float> arcLengthTable = null;	
	[HideInInspector][SerializeField] protected float totalSplineLenght = 0.0f;
	[HideInInspector][SerializeField] protected bool closed = false;
	
	public bool Closed{
		get{return closed;}
		set{SetClosed(value);}
	}

	/**
	* Returns world-space spline lenght.
	*/
	public float Length{
		get{return totalSplineLenght;}
	}

	public virtual void Awake(){
		if (controlPoints == null){
			controlPoints = new List<Vector3>(){Vector3.left, Vector3.zero, Vector3.right, Vector3.right*2};
		}
		if (arcLengthTable == null){
			arcLengthTable = new List<float>();
			RecalculateSplineLenght(0.00001f,7);
		}
	}

	protected abstract void SetClosed(bool closed);

	public abstract void DisplaceControlPoint(int index, Vector3 delta);

	public abstract int GetNumSpans();

	/**
	 * Recalculates spline arc lenght in world space using Gauss-Lobatto adaptive integration. 
	 * @param acc minimum accuray desired (eg 0.00001f)
	 * @param maxevals maximum number of spline evaluations we want to allow per segment.
	 */
	public float RecalculateSplineLenght(float acc, int maxevals){
		
		totalSplineLenght = 0.0f;
		arcLengthTable.Clear();
		arcLengthTable.Add(0);

		float step = 1/(float)(arcLenghtSamples+1);
		
		if (controlPoints.Count >= minPoints){
			for(int k = 1; k < controlPoints.Count-unusedPoints; k += pointStride) {

				Vector3 _p = transform.TransformPoint(controlPoints[k-1]);
				Vector3 p = transform.TransformPoint(controlPoints[k]);
				Vector3 p_ = transform.TransformPoint(controlPoints[k+1]);
				Vector3 p__ = transform.TransformPoint(controlPoints[k+2]);

				for(int i = 0; i <= Mathf.Max(1,arcLenghtSamples); ++i){

					float a = i*step;
					float b = (i+1)*step;

					float segmentLength = GaussLobattoIntegrationStep(_p,p,p_,p__,a,b,
				                                                 EvaluateFirstDerivative3D(_p,p,p_,p__,a).magnitude,
				                                                 EvaluateFirstDerivative3D(_p,p,p_,p__,b).magnitude,0,maxevals,acc);

					totalSplineLenght += segmentLength;

					arcLengthTable.Add(totalSplineLenght);

				}

			}
		}else{
			Debug.LogWarning("Catmull-Rom spline needs at least 4 control points to be defined.");
		}

		return totalSplineLenght;

	}

	/**
	 * One step of the adaptive integration method using Gauss-Lobatto quadrature.
	 * Takes advantage of the fact that the arc lenght of a vector function is equal to the
	 * integral of the magnitude of first derivative.
	 */
	private float GaussLobattoIntegrationStep(Vector3 p1,Vector3 p2,Vector3 p3,Vector3 p4, 
	                                          float a, float b,
	                                          float fa, float fb, int nevals, int maxevals, float acc){

		if (nevals >= maxevals) return 0;

		// Constants used in the algorithm
		float alpha = Mathf.Sqrt(2.0f/3.0f); 
		float beta  = 1.0f/Mathf.Sqrt(5.0f);
		
		// Here the abcissa points and function values for both the 4-point
		// and the 7-point rule are calculated (the points at the end of
		// interval come from the function call, i.e., fa and fb. Also note
		// the 7-point rule re-uses all the points of the 4-point rule.)
		float h=(b-a)/2; 
		float m=(a+b)/2;
		
		float mll=m-alpha*h; 
		float ml =m-beta*h; 
		float mr =m+beta*h; 
		float mrr=m+alpha*h;
		nevals += 5;
		
		float fmll= EvaluateFirstDerivative3D(p1,p2,p3,p4,mll).magnitude;
		float fml = EvaluateFirstDerivative3D(p1,p2,p3,p4,ml).magnitude;
		float fm  = EvaluateFirstDerivative3D(p1,p2,p3,p4,m).magnitude;
		float fmr = EvaluateFirstDerivative3D(p1,p2,p3,p4,mr).magnitude;
		float fmrr= EvaluateFirstDerivative3D(p1,p2,p3,p4,mrr).magnitude;

		// Both the 4-point and 7-point rule integrals are evaluted
		float integral4 = (h/6)*(fa+fb+5*(fml+fmr));
		float integral7 = (h/1470)*(77*(fa+fb)+432*(fmll+fmrr)+625*(fml+fmr)+672*fm);

		// The difference betwen the 4-point and 7-point integrals is the
		// estimate of the accuracy

		if((integral4-integral7) < acc || mll<=a || b<=mrr) 
		{
			if (!(m>a && b>m))
			{
				Debug.LogError("Spline integration reached an interval with no more machine numbers");
			}
			return integral7;
		}else{
			return    GaussLobattoIntegrationStep(p1,p2,p3,p4, a, mll, fa, fmll, nevals, maxevals, acc)  
					+ GaussLobattoIntegrationStep(p1,p2,p3,p4, mll, ml, fmll, fml, nevals, maxevals, acc)
					+ GaussLobattoIntegrationStep(p1,p2,p3,p4, ml, m, fml, fm, nevals, maxevals, acc)
					+ GaussLobattoIntegrationStep(p1,p2,p3,p4, m, mr, fm, fmr, nevals, maxevals, acc)
					+ GaussLobattoIntegrationStep(p1,p2,p3,p4, mr, mrr, fmr, fmrr, nevals, maxevals, acc)
					+ GaussLobattoIntegrationStep(p1,p2,p3,p4, mrr, b, fmrr, fb, nevals, maxevals, acc);
			
		}
	}

	/**
	 * Returns the curve parameter (mu) at a certain length of the curve, using linear interpolation
	 * of the values cached in arcLengthTable.
	 */
	public float GetMuAtLenght(float length){

		if (length <= 0) return 0;
		if (length >= totalSplineLenght) return 1;
		
		int i;
		for (i = 1; i < arcLengthTable.Count; ++i) {
			if (length < arcLengthTable[i]) break; 
		}

		float prevMu = (i-1)/(float)(arcLengthTable.Count-1);
		float nextMu = i/(float)(arcLengthTable.Count-1);

		float s = (length - arcLengthTable[i-1]) / (arcLengthTable[i] - arcLengthTable[i-1]);

		return prevMu + (nextMu - prevMu) * s;
		
	}

	public abstract int GetSpanControlPointForMu(float mu, out float spanMu);
	
	/**
	* Returns spline position at time mu, with 0<=mu<=1 where 0 is the start of the spline
	* and 1 is the end.
	*/
	public Vector3 GetPositionAt(float mu){
		
		if (controlPoints.Count >= minPoints){

			if (!System.Single.IsNaN(mu)){

				float p;
				int i = GetSpanControlPointForMu(mu,out p);
							
				return Evaluate3D(controlPoints[i],
				                    controlPoints[i+1],
				                    controlPoints[i+2],
				                    controlPoints[i+3],p);
			}else{
				return controlPoints[0];
			}

		}else
		//Special case: degenerate spline - line segment (2 or 3 cps)
		if (controlPoints.Count >= 2){
			if (!System.Single.IsNaN(mu)){
				return Vector3.Lerp(controlPoints[0],controlPoints[controlPoints.Count-1],mu);
			}else{
				return controlPoints[0];
			}
		}else 
		//Special case: degenerate spline - point
		if (controlPoints.Count == 1){
			return controlPoints[0];
		}else{
			throw new InvalidOperationException("Cannot get position in Catmull-Rom spline because it has zero control points.");
		}
		
	}
	
	/**
	* Returns normal tangent vector at time mu, with 0<=mu<=1 where 0 is the start of the spline
	* and 1 is the end.
	*/
	public Vector3 GetFirstDerivativeAt(float mu){

		if (controlPoints.Count >= minPoints){

			if (!System.Single.IsNaN(mu)){

				float p;
				int i = GetSpanControlPointForMu(mu,out p);
				
				return EvaluateFirstDerivative3D(controlPoints[i],
								                   controlPoints[i+1],
								                   controlPoints[i+2],
								                   controlPoints[i+3],p);
			}else{
				return controlPoints[controlPoints.Count-1]-controlPoints[0];
			}
		}else
		//Special case: degenerate spline - line segment (2 or 3 cps)
		if (controlPoints.Count >= 2){
			return controlPoints[controlPoints.Count-1]-controlPoints[0];
		}else{
			throw new InvalidOperationException("Cannot get tangent in Catmull-Rom spline because it has zero or one control points.");
		}
	}

	/**
	* Returns acceleration at time mu, with 0<=mu<=1 where 0 is the start of the spline
	* and 1 is the end.
	*/
	public Vector3 GetSecondDerivativeAt(float mu){
		
		if (controlPoints.Count >= minPoints){
			
			if (!System.Single.IsNaN(mu)){
				
				float p;
				int i = GetSpanControlPointForMu(mu,out p);
				
				return EvaluateSecondDerivative3D(controlPoints[i],
				                                    controlPoints[i+1],
				                                    controlPoints[i+2],
				                                    controlPoints[i+3],p);
			}else{
				return Vector3.zero;
			}
		}
		//In all degenerate cases (straight lines or points), acceleration is zero:
		return Vector3.zero;
	}
		
		
	/**
	* 3D spline interpolation
	*/
	private Vector3 Evaluate3D(Vector3 y0, Vector3 y1, Vector3 y2, Vector3 y3, float mu){
		
		return new Vector3(Evaluate1D(y0.x,y1.x,y2.x,y3.x,mu),
			               Evaluate1D(y0.y,y1.y,y2.y,y3.y,mu),
			               Evaluate1D(y0.z,y1.z,y2.z,y3.z,mu));
		
	}
	
	/**
	* 1D spline interpolation
	*/
	protected abstract float Evaluate1D(float y0, float y1, float y2, float y3, float mu);

	/**
	* 3D spline first derivative
	*/
	private Vector3 EvaluateFirstDerivative3D(Vector3 y0, Vector3 y1, Vector3 y2, Vector3 y3, float mu){
		
		return new Vector3(EvaluateFirstDerivative1D(y0.x,y1.x,y2.x,y3.x,mu),
		                   EvaluateFirstDerivative1D(y0.y,y1.y,y2.y,y3.y,mu),
		                   EvaluateFirstDerivative1D(y0.z,y1.z,y2.z,y3.z,mu));
		
	}

	/**
	* 1D spline second derivative
	*/
	protected abstract float EvaluateFirstDerivative1D(float y0, float y1, float y2, float y3, float mu);

	/**
	* 3D spline second derivative
	*/
	private Vector3 EvaluateSecondDerivative3D(Vector3 y0, Vector3 y1, Vector3 y2, Vector3 y3, float mu){
		
		return new Vector3(EvaluateSecondDerivative1D(y0.x,y1.x,y2.x,y3.x,mu),
		                   EvaluateSecondDerivative1D(y0.y,y1.y,y2.y,y3.y,mu),
		                   EvaluateSecondDerivative1D(y0.z,y1.z,y2.z,y3.z,mu));
		
	}
	
	/**
	* 1D spline second derivative
	*/
	protected abstract float EvaluateSecondDerivative1D(float y0, float y1, float y2, float y3, float mu);
	

}
}
