using UnityEngine;
using System.Collections.Generic;
using System.Threading;

//-----------------------------------------------------------------------------
// Copyright 2014-2020 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProDeckLink
{
	public class DeckLinkAudioOutput : MonoBehaviour
	{
		private List<Device> _registeredDevices;
		private Mutex _mutex;

		void Awake()
		{
			_registeredDevices = new List<Device>();
			_mutex = new Mutex();
		}

		// Use this for initialization
		void Start()
		{
			
		}

		// Update is called once per frame
		void Update()
		{
		}

		public void RegisterDevice(int deviceIndex)
		{
			_mutex.WaitOne();

			DeckLinkManager manager = DeckLinkManager.Instance;
			if (manager != null)
			{
				bool contains = false;
				foreach (Device device in _registeredDevices)
				{
					if (deviceIndex == device.DeviceIndex)
					{
						contains = true;
						break;
					}
				}

				if (!contains)
				{
					Device device = manager.GetDeviceByDeviceIndex(deviceIndex);
					if (device != null)
					{
						_registeredDevices.Add(device);
					}
				}
			}
			//Debug.Log("devices: " + _registeredDevices.Count);
			_mutex.ReleaseMutex();
		}

		public void UnregisterDevice(int deviceIndex)
		{
			_mutex.WaitOne();
			DeckLinkManager manager = DeckLinkManager.Instance;
			if (manager != null)
			{
				Device device = manager.GetDeviceByDeviceIndex(deviceIndex);
				if (device != null)
				{
					if (_registeredDevices.Contains(device))
					{
						_registeredDevices.Remove(device);
					}
				}
			}
			//Debug.Log("unreg: " + _registeredDevices.Count);
			_mutex.ReleaseMutex();
		}

		public void OnAudioFilterRead(float[] data, int channels)
		{
			DeckLinkManager manager = DeckLinkManager.Instance;
			if (manager == null) return;

			_mutex.WaitOne();
			foreach (Device device in _registeredDevices)
			{
				if (device == null)
				{
					break;
				}

				if (device.OutputAudioChannels > 0)
				{
					DeckLinkPlugin.OutputAudio(device.DeviceIndex, data, data.Length, channels);
				}
			}
			_mutex.ReleaseMutex();
		}
	}
}

