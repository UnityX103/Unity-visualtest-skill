using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;
using FFmpegOut;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace NZ.VisualTest
{
    /// <summary>
    /// 视频视觉测试基类。
    /// 提供测试辅助相机、GUI 操作提示、输入模拟、Camera/Texture 视频录制和 checkpoint 主动采帧。
    /// </summary>
    public abstract class VisualTestBase
    {
        private GameObject _cameraObject;
        private GameObject _guiHelperObject;
        private VisualTestGuiHelper _guiHelper;
        private RuntimeFFmpegRecorder _runtimeRecorder;

        protected virtual string TestName => GetType().Name;
        protected virtual bool UseDedicatedTestCamera => true;
        protected virtual bool RecordOnSetUp => true;
        protected virtual float RecorderFrameRate => 30f;

        protected void StartRecordingNow()
        {
            StartRecorder();
        }

        protected IEnumerator CaptureVideoCheckpoint(float holdSeconds = 1f)
        {
            yield return null;

            var stableDuration = Mathf.Max(0f, holdSeconds);
            if (stableDuration <= 0f)
            {
                yield break;
            }

            var deadline = Time.realtimeSinceStartup + stableDuration;
            while (Time.realtimeSinceStartup < deadline)
            {
                _runtimeRecorder?.CaptureFrameNow();
                yield return null;
            }
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (UseDedicatedTestCamera)
            {
                SetUpCamera();
            }

            SetUpGuiHelper();
            yield return null;

            if (RecordOnSetUp)
            {
                StartRecorder();
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            StopRecorder();

            if (_guiHelperObject != null)
            {
                UnityEngine.Object.Destroy(_guiHelperObject);
                _guiHelperObject = null;
                _guiHelper = null;
            }

            if (_cameraObject != null)
            {
                UnityEngine.Object.Destroy(_cameraObject);
                _cameraObject = null;
            }

            yield return null;
        }

        protected void LogInputAction(string text)
        {
            _guiHelper?.LogAction(text);
            Debug.Log($"[VisualTest] {text}");
        }

        protected IEnumerator SimulateKey(Key key, string displayName = null)
        {
            var label = displayName ?? key.ToString();
            LogInputAction($"按键: {label}");

            var keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            InputSystem.Update();
            yield return null;

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.Update();
            InputSystem.RemoveDevice(keyboard);
            yield return null;
        }

        protected IEnumerator SimulateMouseButton(int button, string displayName = null)
        {
            var label = displayName ?? $"鼠标按键{button}";
            LogInputAction($"点击: {label}");

            var mouse = InputSystem.AddDevice<Mouse>();
            var pressedState = new MouseState();
            var releasedState = new MouseState();

            switch (button)
            {
                case 1:
                    pressedState = pressedState.WithButton(MouseButton.Right, true);
                    break;
                case 2:
                    pressedState = pressedState.WithButton(MouseButton.Middle, true);
                    break;
                default:
                    pressedState = pressedState.WithButton(MouseButton.Left, true);
                    break;
            }

            InputSystem.QueueStateEvent(mouse, pressedState);
            InputSystem.Update();
            yield return null;

            InputSystem.QueueStateEvent(mouse, releasedState);
            InputSystem.Update();
            InputSystem.RemoveDevice(mouse);
            yield return null;
        }

        protected virtual CaptureSource ResolveCaptureSource()
        {
            var preferredSize = ResolvePreferredCaptureSize();
            var textureSource = ResolveRawImageCaptureSource(preferredSize.x, preferredSize.y);
            if (textureSource != null)
            {
                return textureSource;
            }

            var cameraSource = ResolveCameraCaptureSource(preferredSize.x, preferredSize.y);
            if (cameraSource != null)
            {
                return cameraSource;
            }

            var camera = ResolveCaptureCamera();
            return camera == null ? null : CaptureSource.FromCamera(camera, DescribeCamera(camera),
                preferredSize.x, preferredSize.y);
        }

        protected virtual Vector2Int ResolvePreferredCaptureSize()
        {
            var rawImages = UnityEngine.Object.FindObjectsOfType<RawImage>(true);
            foreach (var rawImage in rawImages)
            {
                if (rawImage == null)
                {
                    continue;
                }

                if (rawImage.texture != null)
                {
                    return new Vector2Int(rawImage.texture.width, rawImage.texture.height);
                }

                var rectTransform = rawImage.transform as RectTransform;
                if (rectTransform != null)
                {
                    var rect = rectTransform.rect;
                    if (rect.width > 0f && rect.height > 0f)
                    {
                        return new Vector2Int(Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height));
                    }
                }
            }

            return new Vector2Int(Mathf.Max(8, Screen.width), Mathf.Max(8, Screen.height));
        }

        protected virtual Camera ResolveCaptureCamera()
        {
            var dedicatedCamera = _cameraObject != null ? _cameraObject.GetComponent<Camera>() : null;
            var allCameras = UnityEngine.Object.FindObjectsOfType<Camera>(true);

            Camera selectedCamera = null;
            var selectedArea = -1;

            foreach (var camera in allCameras)
            {
                if (camera == null || camera == dedicatedCamera || camera.targetTexture == null)
                {
                    continue;
                }

                var currentArea = camera.targetTexture.width * camera.targetTexture.height;
                if (currentArea > selectedArea)
                {
                    selectedCamera = camera;
                    selectedArea = currentArea;
                }
            }

            if (selectedCamera != null)
            {
                return selectedCamera;
            }

            if (Camera.main != null && Camera.main != dedicatedCamera)
            {
                return Camera.main;
            }

            foreach (var camera in allCameras)
            {
                if (camera != null && camera != dedicatedCamera)
                {
                    return camera;
                }
            }

            return dedicatedCamera;
        }

        protected virtual Camera ResolveFrameTriggerCamera()
        {
            var dedicatedCamera = _cameraObject != null ? _cameraObject.GetComponent<Camera>() : null;
            var allCameras = UnityEngine.Object.FindObjectsOfType<Camera>(true);
            Camera selectedCamera = null;

            foreach (var camera in allCameras)
            {
                if (camera == null || camera == dedicatedCamera || !camera.enabled || !camera.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (selectedCamera == null || camera.depth > selectedCamera.depth)
                {
                    selectedCamera = camera;
                }
            }

            return selectedCamera ?? ResolveCaptureCamera();
        }

        private void SetUpCamera()
        {
            _cameraObject = new GameObject("VisualTest_Camera");
            var camera = _cameraObject.AddComponent<Camera>();
            _cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            _cameraObject.transform.rotation = Quaternion.identity;
            camera.backgroundColor = Color.black;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.cullingMask = -1;
        }

        private void SetUpGuiHelper()
        {
            _guiHelperObject = new GameObject("VisualTest_GuiHelper");
            _guiHelper = _guiHelperObject.AddComponent<VisualTestGuiHelper>();
        }

        private string GetVideoOutputDirectory()
        {
            return Path.Combine(Application.persistentDataPath, "TestOutput", GetType().Name, "Video");
        }

        private string BuildTimestampedFileName()
        {
            return $"{TestName}_{DateTime.Now:yyyyMMdd_HHmmss}";
        }

        private void StartRecorder()
        {
            if (_runtimeRecorder != null)
            {
                Debug.Log("[VisualTestBase] 录制器已启动，忽略重复启动。");
                return;
            }

            try
            {
                var ffmpegExecutablePath = FFmpegPipe.ExecutablePath;
                if (string.IsNullOrEmpty(ffmpegExecutablePath))
                {
                    Debug.LogWarning("[VisualTestBase] 视频录制启动失败：未找到 ffmpeg 可执行文件。");
                    return;
                }

                var captureSource = ResolveCaptureSource();
                if (captureSource == null)
                {
                    Debug.LogWarning("[VisualTestBase] 视频录制启动失败：未找到可用于录制的输出纹理或 Camera。");
                    return;
                }

                var outputDirectory = GetVideoOutputDirectory();
                Directory.CreateDirectory(outputDirectory);

                var outputPath = Path.Combine(outputDirectory, $"{BuildTimestampedFileName()}.mp4");
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                }

                var recorderHost = captureSource.Host != null ? captureSource.Host : _guiHelperObject;
                if (recorderHost == null)
                {
                    Debug.LogWarning("[VisualTestBase] 视频录制启动失败：缺少录制器挂载对象。");
                    return;
                }

                _runtimeRecorder = recorderHost.GetComponent<RuntimeFFmpegRecorder>();
                if (_runtimeRecorder == null)
                {
                    _runtimeRecorder = recorderHost.AddComponent<RuntimeFFmpegRecorder>();
                }

                _runtimeRecorder.StartRecording(captureSource, outputPath, ffmpegExecutablePath, RecorderFrameRate);
                Debug.Log($"[VisualTestBase] 视频录制已启动：{outputPath}，录制源：{captureSource.Description}");
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[VisualTestBase] 视频录制启动失败，测试将继续但不录制视频。原因：{exception.Message}");
                StopRecorder();
            }
        }

        private void StopRecorder()
        {
            if (_runtimeRecorder == null)
            {
                return;
            }

            _runtimeRecorder.StopRecording();
            UnityEngine.Object.Destroy(_runtimeRecorder);
            _runtimeRecorder = null;
        }

        private CaptureSource ResolveCameraCaptureSource(int preferredWidth, int preferredHeight)
        {
            var camera = ResolveFrameTriggerCamera();
            return camera == null ? null : CaptureSource.FromCamera(camera, DescribeCamera(camera), preferredWidth,
                preferredHeight);
        }

        private CaptureSource ResolveRawImageCaptureSource(int preferredWidth, int preferredHeight)
        {
            var rawImages = UnityEngine.Object.FindObjectsOfType<RawImage>(true);
            CaptureSource selectedSource = null;
            var selectedPriority = -1;
            var selectedArea = -1;

            foreach (var rawImage in rawImages)
            {
                if (rawImage == null || rawImage.texture == null)
                {
                    continue;
                }

                var texture = rawImage.texture;
                var isRenderTexture = texture is RenderTexture;
                var priority = rawImage.gameObject.activeInHierarchy && isRenderTexture ? 2
                    : isRenderTexture ? 1
                    : 0;
                var area = texture.width * texture.height;

                if (priority < selectedPriority)
                {
                    continue;
                }

                if (priority == selectedPriority && area <= selectedArea)
                {
                    continue;
                }

                selectedPriority = priority;
                selectedArea = area;
                selectedSource = CaptureSource.FromTexture(
                    texture,
                    $"RawImage:{rawImage.name}->{texture.name}({texture.width}x{texture.height})",
                    rawImage.gameObject,
                    ResolveFrameTriggerCamera(),
                    preferredWidth,
                    preferredHeight);
            }

            return selectedSource;
        }

        private static string DescribeCamera(Camera camera)
        {
            if (camera == null)
            {
                return "null";
            }

            if (camera.targetTexture == null)
            {
                return $"{camera.name}(screen)";
            }

            return $"{camera.name}({camera.targetTexture.width}x{camera.targetTexture.height})";
        }

        protected sealed class CaptureSource
        {
            public Camera Camera { get; private set; }
            public Texture Texture { get; private set; }
            public string Description { get; private set; }
            public GameObject Host { get; private set; }
            public Camera TriggerCamera { get; private set; }
            public int PreferredWidth { get; private set; }
            public int PreferredHeight { get; private set; }

            public static CaptureSource FromCamera(Camera camera, string description, int preferredWidth,
                int preferredHeight)
            {
                return new CaptureSource
                {
                    Camera = camera,
                    Description = description,
                    Host = camera != null ? camera.gameObject : null,
                    TriggerCamera = camera,
                    PreferredWidth = preferredWidth,
                    PreferredHeight = preferredHeight
                };
            }

            public static CaptureSource FromTexture(Texture texture, string description, GameObject host,
                Camera triggerCamera, int preferredWidth, int preferredHeight)
            {
                return new CaptureSource
                {
                    Texture = texture,
                    Description = description,
                    Host = host,
                    TriggerCamera = triggerCamera,
                    PreferredWidth = preferredWidth,
                    PreferredHeight = preferredHeight
                };
            }
        }

        private sealed class RuntimeFFmpegRecorder : MonoBehaviour
        {
            private DirectFfmpegSession _session;
            private Camera _captureCamera;
            private Texture _captureTexture;
            private RenderTexture _outputBridgeTexture;
            private Texture2D _readbackTexture;
            private Coroutine _textureCaptureCoroutine;
            private bool _captureFromCameraOutput;
            private float _frameRate;
            private int _frameCount;
            private float _startTime;
            private int _frameDropCount;
            private int _submittedFrameCount;
            private int _recordLoopTickCount;

            private float FrameTime => _startTime + (_frameCount - 0.5f) / _frameRate;

            public void StartRecording(CaptureSource captureSource, string outputPath, string ffmpegExecutablePath,
                float frameRate)
            {
                StopRecording();

                if (captureSource == null)
                {
                    throw new ArgumentNullException(nameof(captureSource));
                }

                _frameRate = Mathf.Max(1f, frameRate);
                _captureFromCameraOutput = captureSource.Camera != null;
                _captureCamera = captureSource.Camera;

                var outputWidth = Mathf.Max(8, captureSource.PreferredWidth);
                var outputHeight = Mathf.Max(8, captureSource.PreferredHeight);

                if (!_captureFromCameraOutput)
                {
                    _captureTexture = captureSource.Texture;
                    if (_captureTexture == null)
                    {
                        throw new InvalidOperationException("录制源没有可用的 Texture。");
                    }

                    outputWidth = Mathf.Max(8, _captureTexture.width);
                    outputHeight = Mathf.Max(8, _captureTexture.height);
                }

                EnsureReadbackTargets(outputWidth, outputHeight);
                _session = new DirectFfmpegSession(ffmpegExecutablePath, outputPath, outputWidth,
                    outputHeight, _frameRate);

                _frameCount = 0;
                _frameDropCount = 0;
                _submittedFrameCount = 0;
                _recordLoopTickCount = 0;
                _startTime = Time.realtimeSinceStartup;

                if (!_captureFromCameraOutput)
                {
                    _textureCaptureCoroutine = StartCoroutine(CaptureTextureFrames());
                }
            }

            public void StopRecording()
            {
                if (_textureCaptureCoroutine != null)
                {
                    StopCoroutine(_textureCaptureCoroutine);
                    _textureCaptureCoroutine = null;
                }

                if (_session != null)
                {
                    _session.Close();
                    _session = null;
                }

                Debug.Log($"[VisualTestBase] 录制结束：循环次数={_recordLoopTickCount}，提交帧数={_submittedFrameCount}");

                if (_outputBridgeTexture != null)
                {
                    _outputBridgeTexture.Release();
                    Destroy(_outputBridgeTexture);
                    _outputBridgeTexture = null;
                }

                if (_readbackTexture != null)
                {
                    Destroy(_readbackTexture);
                    _readbackTexture = null;
                }

                _captureCamera = null;
                _captureTexture = null;
                _captureFromCameraOutput = false;
            }

            public void CaptureFrameNow()
            {
                if (_captureFromCameraOutput)
                {
                    TryCaptureCameraFrame();
                    return;
                }

                TryCaptureTextureFrame();
            }

            private void EnsureReadbackTargets(int width, int height)
            {
                if (_readbackTexture != null &&
                    _readbackTexture.width == width &&
                    _readbackTexture.height == height &&
                    _outputBridgeTexture != null &&
                    _outputBridgeTexture.width == width &&
                    _outputBridgeTexture.height == height)
                {
                    return;
                }

                if (_outputBridgeTexture != null)
                {
                    _outputBridgeTexture.Release();
                    Destroy(_outputBridgeTexture);
                    _outputBridgeTexture = null;
                }

                if (_readbackTexture != null)
                {
                    Destroy(_readbackTexture);
                    _readbackTexture = null;
                }

                _outputBridgeTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
                _outputBridgeTexture.Create();
                _readbackTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            }

            private void PushFrameWithTiming(Texture source)
            {
                var gap = Time.realtimeSinceStartup - FrameTime;
                var delta = 1f / _frameRate;

                if (gap < 0f)
                {
                    return;
                }

                if (gap < delta)
                {
                    PushFrameToEncoder(source);
                    _submittedFrameCount++;
                    _frameCount++;
                    return;
                }

                if (gap < delta * 2f)
                {
                    PushFrameToEncoder(source);
                    PushFrameToEncoder(source);
                    _submittedFrameCount += 2;
                    _frameCount += 2;
                    return;
                }

                WarnFrameDrop();
                PushFrameToEncoder(source);
                _submittedFrameCount++;
                _frameCount += Mathf.Max(1, Mathf.FloorToInt(gap * _frameRate));
            }

            private IEnumerator CaptureTextureFrames()
            {
                while (_session != null)
                {
                    yield return null;
                    TryCaptureTextureFrame();
                }
            }

            private void TryCaptureTextureFrame()
            {
                if (_session == null)
                {
                    return;
                }

                _recordLoopTickCount++;

                if (_captureTexture == null)
                {
                    Debug.LogWarning("[VisualTestBase] 录制中断：录制源纹理已丢失。");
                    StopRecording();
                    return;
                }

                PushFrameWithTiming(_captureTexture);
            }

            private void TryCaptureCameraFrame()
            {
                if (_session == null)
                {
                    return;
                }

                _recordLoopTickCount++;

                if (_captureCamera != null)
                {
                    var previousTargetTexture = _captureCamera.targetTexture;
                    _captureCamera.targetTexture = _outputBridgeTexture;
                    _captureCamera.Render();
                    _captureCamera.targetTexture = previousTargetTexture;
                    PushFrameWithTiming(_outputBridgeTexture);
                    return;
                }

                if (_outputBridgeTexture == null)
                {
                    return;
                }

                ScreenCapture.CaptureScreenshotIntoRenderTexture(_outputBridgeTexture);
                PushFrameWithTiming(_outputBridgeTexture);
            }

            private void OnRenderImage(RenderTexture source, RenderTexture destination)
            {
                Graphics.Blit(source, destination);

                if (!_captureFromCameraOutput || _session == null || source == null)
                {
                    return;
                }

                _recordLoopTickCount++;
                PushFrameWithTiming(source);
            }

            private void PushFrameToEncoder(Texture source)
            {
                if (_session == null || _readbackTexture == null)
                {
                    return;
                }

                var readableRenderTexture = GetReadableRenderTexture(source);
                if (readableRenderTexture == null)
                {
                    return;
                }

                var previousActive = RenderTexture.active;
                RenderTexture.active = readableRenderTexture;
                _readbackTexture.ReadPixels(
                    new Rect(0, 0, readableRenderTexture.width, readableRenderTexture.height),
                    0,
                    0,
                    false);
                _readbackTexture.Apply(false, false);
                RenderTexture.active = previousActive;

                _session.PushFrame(_readbackTexture.GetRawTextureData());
            }

            private RenderTexture GetReadableRenderTexture(Texture source)
            {
                if (_outputBridgeTexture == null)
                {
                    return null;
                }

                if (source == _outputBridgeTexture)
                {
                    return _outputBridgeTexture;
                }

                Graphics.Blit(source, _outputBridgeTexture);
                return _outputBridgeTexture;
            }

            private void OnDestroy()
            {
                StopRecording();
            }

            private void WarnFrameDrop()
            {
                if (++_frameDropCount != 10)
                {
                    return;
                }

                Debug.LogWarning("显著丢帧，输出视频的时间稳定性可能受影响，建议降低录制帧率。");
            }
        }

        private sealed class DirectFfmpegSession
        {
            private readonly Process _process;
            private readonly Stream _inputStream;

            public DirectFfmpegSession(string ffmpegExecutablePath, string outputPath, int width, int height,
                float frameRate)
            {
                if (string.IsNullOrEmpty(ffmpegExecutablePath))
                {
                    throw new ArgumentException("缺少 ffmpeg 可执行文件路径。", nameof(ffmpegExecutablePath));
                }

                var arguments =
                    $"-y -f rawvideo -vcodec rawvideo -pixel_format rgba -video_size {width}x{height} " +
                    $"-framerate {frameRate} -i - -vf vflip -c:v libx264 -preset veryfast -pix_fmt yuv420p \"{outputPath}\"";

                var startInfo = new ProcessStartInfo
                {
                    FileName = ffmpegExecutablePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                _process = Process.Start(startInfo);
                if (_process == null)
                {
                    throw new InvalidOperationException("ffmpeg 进程启动失败。");
                }

                _inputStream = _process.StandardInput.BaseStream;
            }

            public void PushFrame(byte[] rawRgbaBytes)
            {
                if (_process == null || _process.HasExited || rawRgbaBytes == null || rawRgbaBytes.Length == 0)
                {
                    return;
                }

                _inputStream.Write(rawRgbaBytes, 0, rawRgbaBytes.Length);
            }

            public void Close()
            {
                if (_process == null)
                {
                    return;
                }

                try
                {
                    _inputStream.Flush();
                    _inputStream.Close();
                }
                catch
                {
                }

                _process.WaitForExit(30000);
                var errorOutput = _process.StandardError.ReadToEnd();
                var exitCode = _process.ExitCode;

                _process.Dispose();

                if (exitCode != 0)
                {
                    Debug.LogWarning($"[VisualTestBase] ffmpeg 退出码异常：{exitCode}\n{errorOutput}");
                    return;
                }

                if (!string.IsNullOrWhiteSpace(errorOutput))
                {
                    Debug.LogWarning($"[VisualTestBase] ffmpeg 输出信息：\n{errorOutput}");
                }
            }
        }
    }
}
