using System.Collections.Generic;
using GTA.Native;

namespace RageCoop.Client{

public static class SoundIdPool
{
	private const int MaxPoolSize = 90;

	private static Queue<int> soundIdQueue = new Queue<int>();

	public static int GetSoundId()
	{
		if (soundIdQueue.Count < 90)
		{
			int num = Function.Call<int>(Hash.GET_SOUND_ID);
			soundIdQueue.Enqueue(num);
			return num;
		}
		int num2 = soundIdQueue.Dequeue();
		Function.Call(Hash.STOP_SOUND, num2);
		soundIdQueue.Enqueue(num2);
		return num2;
	}

	public static void Clear()
	{
		while (soundIdQueue.Count > 0)
		{
			int num = soundIdQueue.Dequeue();
			Function.Call(Hash.STOP_SOUND, num);
			Function.Call(Hash.RELEASE_SOUND_ID, num);
		}
	}
}
}