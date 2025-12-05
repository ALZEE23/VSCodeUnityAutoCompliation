using System;
using UnityEditor;
using UnityEngine;
using UnityEditor.Compilation;
using System.Net;
using UnityEditorInternal;
using System.Net.Sockets;
using System.Threading;

namespace PostcyberPunk.AutoCompilation
{
	[InitializeOnLoad]
	public static class AutoCompilation
	{
		private static HttpListener listener;
		private static bool needUpdate;
		private static string port = "10245";
		private static IAsyncResult _result;
		private static bool isEditorFocused = true;
		private static double lastChangeTime = 0;
		private static double debounceSeconds = 1.0; // nunggu 1 detik setelah perubahan terakhir

		static AutoCompilation()
		{
			if (!SessionState.GetBool("DisableAutoCompilation", false))
			{
				needUpdate = false;
				CompilationPipeline.compilationFinished += OnCompilationFinished;
				EditorApplication.quitting += _closeListener;
				EditorApplication.update += onUpdate;
			}
		}

		private static void _createListener()
		{
			if (listener != null) return;

			try
			{
				listener = new HttpListener();
				listener.Prefixes.Add("http://127.0.0.1:" + port + "/refresh/");
				listener.Start();
				_result = listener.BeginGetContext(new AsyncCallback(OnRequest), listener);
			}
			catch (Exception e)
			{
				Debug.LogError("Auto Compilation starting failed: " + e);
			}
		}

		private static void OnRequest(IAsyncResult result)
		{
			if (listener.IsListening && !EditorApplication.isCompiling)
			{
				listener.EndGetContext(result);
				needUpdate = true;
				lastChangeTime = EditorApplication.timeSinceStartup;
				_result = listener.BeginGetContext(new AsyncCallback(OnRequest), listener);
			}
		}

		private static void _closeListener()
		{
			if (listener == null) return;
			listener.Stop();
			listener.Close();
			listener = null;
		}

		private static void onUpdate()
		{
			// Cek focus editor
			if (InternalEditorUtility.isApplicationActive != isEditorFocused)
			{
				isEditorFocused = InternalEditorUtility.isApplicationActive;
				if (isEditorFocused)
				{
					// kalau balik fokus, cek update
					if (needUpdate)
					{
						_doCompile();
					}
				}
			}

			// Debounce & compile hanya kalau editor fokus
			if (isEditorFocused && needUpdate)
			{
				double elapsed = EditorApplication.timeSinceStartup - lastChangeTime;
				if (elapsed >= debounceSeconds && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
				{
					_doCompile();
				}
			}
		}

		private static void _doCompile()
		{
			needUpdate = false;
			AssetDatabase.Refresh();
			Debug.Log("Auto Compilation executed.");
		}

		[MenuItem("Tools/AutoCompilation/Toggle Auto-Compilation")]
		public static void ToggleAutoCompilation()
		{
			bool toggle = SessionState.GetBool("DisableAutoCompilation", false);
			if (toggle)
			{
				_createListener();
			}
			else
			{
				_closeListener();
			}
			SessionState.SetBool("DisableAutoCompilation", !toggle);
			Debug.Log("Auto Compilation is " + (!toggle ? "Off" : "On"));
		}

		private static void OnCompilationFinished(object _)
		{
			if (isEditorFocused)
			{
				_createListener();
			}
		}
	}
}
