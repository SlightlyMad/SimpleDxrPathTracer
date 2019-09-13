using UnityEngine;
using UnityEngine.Experimental.Rendering;

public class DXRCamera : MonoBehaviour
{	
	public Color SkyColor = Color.blue;
	public Color GroundColor = Color.gray;
	
	private Camera _camera;
	// target texture for raytracing
	private RenderTexture _dxrTarget;
	// textures for accumulation
	private RenderTexture _accumulationTarget1;
	private RenderTexture _accumulationTarget2;

	// scene structure for raytracing
	private RayTracingAccelerationStructure _rtas;

	// raytracing shader
	private RayTracingShader _rayTracingShader;
	// helper material to accumulate raytracing results
	private Material _accumulationMaterial;

	private Matrix4x4 _cameraWorldMatrix;

	private int _frameIndex;

	private void Start()
	{
		Debug.Log("Raytracing support: " + SystemInfo.supportsRayTracing);

		_camera = GetComponent<Camera>();

		_dxrTarget = new RenderTexture(_camera.pixelWidth, _camera.pixelHeight, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Default);
		_dxrTarget.enableRandomWrite = true;
		_dxrTarget.Create();

		_accumulationTarget1 = new RenderTexture(_dxrTarget);
		_accumulationTarget2 = new RenderTexture(_dxrTarget);		

		// build scene for raytracing
		InitRaytracingAccelerationStructure();
		
		_rayTracingShader = Resources.Load<RayTracingShader>("RayTracingShader");

		_rayTracingShader.SetAccelerationStructure("_RaytracingAccelerationStructure", _rtas);
		_rayTracingShader.SetTexture("_DxrTarget", _dxrTarget);
		// set shader pass name that will be used during raytracing
		_rayTracingShader.SetShaderPass("DxrPass");
			   
		// update raytracing parameters
		UpdateParameters();		

		_accumulationMaterial = new Material(Shader.Find("Hidden/Accumulation"));
	}

	private void Update()
	{
		// update parameters if camera moved
		if(_cameraWorldMatrix != _camera.transform.localToWorldMatrix)
		{
			UpdateParameters();
		}

		// update parameters manually. after material or scene change
		if (Input.GetKeyDown(KeyCode.Space))
		{
			UpdateParameters();			
		}
	}

	private void UpdateParameters()
	{
		// update raytracing scene, in case something moved
		_rtas.Update();

		// frustum corners for current camera transform
		Vector3 bottomLeft = _camera.ViewportToWorldPoint(new Vector3(0, 0, _camera.farClipPlane)).normalized;
		Vector3 topLeft = _camera.ViewportToWorldPoint(new Vector3(0, 1, _camera.farClipPlane)).normalized;
		Vector3 bottomRight = _camera.ViewportToWorldPoint(new Vector3(1, 0, _camera.farClipPlane)).normalized;
		Vector3 topRight = _camera.ViewportToWorldPoint(new Vector3(1, 1, _camera.farClipPlane)).normalized;

		// update camera, environment parameters
		_rayTracingShader.SetVector("_SkyColor", SkyColor.gamma);
		_rayTracingShader.SetVector("_GroundColor", GroundColor.gamma);

		_rayTracingShader.SetVector("_TopLeftFrustumDir", topLeft);
		_rayTracingShader.SetVector("_TopRightFrustumDir", topRight);
		_rayTracingShader.SetVector("_BottomLeftFrustumDir", bottomLeft);
		_rayTracingShader.SetVector("_BottomRightFrustumDir", bottomRight);

		_rayTracingShader.SetVector("_CameraPos", _camera.transform.position);

		_cameraWorldMatrix = _camera.transform.localToWorldMatrix;

		// reset accumulation frame counter
		_frameIndex = 0;
	}

	private void InitRaytracingAccelerationStructure()
	{
		RayTracingAccelerationStructure.RASSettings settings = new RayTracingAccelerationStructure.RASSettings();
		// include all layers
		settings.layerMask = ~0;
		// enable automatic updates
		settings.managementMode = RayTracingAccelerationStructure.ManagementMode.Automatic;
		// include all renderer types
		settings.rayTracingModeMask = RayTracingAccelerationStructure.RayTracingModeMask.Everything;

		_rtas = new RayTracingAccelerationStructure(settings);
		
		// collect all objects in scene and add them to raytracing scene
		Renderer[] renderers = FindObjectsOfType<Renderer>();
		foreach(Renderer r in renderers)
			_rtas.AddInstance(r);

		// build raytrasing scene
		_rtas.Build();
	}

	[ImageEffectOpaque]
	private void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		// update frame index and start path tracer
		_rayTracingShader.SetInt("_FrameIndex", _frameIndex);
		// start one thread for each pixel on screen
		_rayTracingShader.Dispatch("MyRaygenShader", _camera.pixelWidth, _camera.pixelHeight, 1, _camera);

		// update accumulation material
		_accumulationMaterial.SetTexture("_CurrentFrame", _dxrTarget);
		_accumulationMaterial.SetTexture("_Accumulation", _accumulationTarget1);
		_accumulationMaterial.SetInt("_FrameIndex", _frameIndex++);

		// accumulate current raytracing result
		Graphics.Blit(_dxrTarget, _accumulationTarget2, _accumulationMaterial);
		// display result on screen
		Graphics.Blit(_accumulationTarget2, destination);

		// switch accumulate textures
		var temp = _accumulationTarget1;
		_accumulationTarget1 = _accumulationTarget2;
		_accumulationTarget2 = temp;
	}

	private void OnGUI()
	{
		// display samples per pixel
		//GUILayout.Label("SPP: " + _frameIndex);
	}

	private void OnDestroy()
	{		
		// cleanup
		_rtas.Release();
		_dxrTarget.Release();
		_accumulationTarget1.Release();
		_accumulationTarget2.Release();
	}
}
