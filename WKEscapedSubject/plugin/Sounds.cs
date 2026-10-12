using System;
using System.IO;
using UnityEngine;
using UnityEngine.Audio;

namespace WKEscapedSubject
{
    // 플러그인 폴더 Assets/Sounds/*.wav 재생. WAV(16bit PCM)를 직접 읽어 AudioClip으로 만든다
    internal static class Sounds
    {
        public static AudioMixerGroup Mixer; // 게임 효과음 볼륨을 따르도록 바나클 훅 소리의 믹서 그룹을 빌려 씀

        public static AudioClip LoadWav(string file)
        {
            byte[] b = AssetStore.Read("Sounds/" + file);
            if (b == null) { Plugin.Log.LogWarning("Sound not found: " + file); return null; }
            try
            {
                int channels = 0, rate = 0, bits = 0, dataPos = -1, dataLen = 0;
                int p = 12; // "RIFF" size "WAVE" 다음부터 청크
                while (p + 8 <= b.Length)
                {
                    string id = System.Text.Encoding.ASCII.GetString(b, p, 4);
                    int len = BitConverter.ToInt32(b, p + 4);
                    if (id == "fmt ")
                    {
                        channels = BitConverter.ToInt16(b, p + 10);
                        rate = BitConverter.ToInt32(b, p + 12);
                        bits = BitConverter.ToInt16(b, p + 22);
                    }
                    else if (id == "data") { dataPos = p + 8; dataLen = Math.Min(len, b.Length - dataPos); break; }
                    p += 8 + len + (len & 1);
                }
                if (dataPos < 0 || bits != 16 || channels < 1) { Plugin.Log.LogWarning("지원하지 않는 WAV 형식(16bit PCM만): " + file); return null; }

                int samples = dataLen / 2;
                var data = new float[samples];
                for (int i = 0; i < samples; i++) data[i] = BitConverter.ToInt16(b, dataPos + i * 2) / 32768f;
                AudioClip clip = AudioClip.Create(Path.GetFileNameWithoutExtension(file), samples / channels, channels, rate, false);
                // Unity 6의 SetData에는 ReadOnlySpan 오버로드가 섞여 있어 net472로는 직접 호출이 안 되므로 float[] 버전을 리플렉션으로 부른다
                typeof(AudioClip).GetMethod("SetData", new[] { typeof(float[]), typeof(int) }).Invoke(clip, new object[] { data, 0 });
                clip.hideFlags = HideFlags.DontUnloadUnusedAsset;
                Plugin.Log.LogInfo(string.Format("소리 로드: {0} ({1:0.00}초)", file, clip.length));
                return clip;
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("소리 로드 실패 " + file + ": " + e.Message);
                return null;
            }
        }

        // 2D 효과음 재생기 하나 (재생·중지 가능)
        public class Player
        {
            readonly string file;
            AudioClip clip;
            bool loaded;
            AudioSource src;

            public Player(string file) { this.file = file; }

            public bool IsPlaying { get { return src != null && src.isPlaying; } }

            public void Play(GameObject host, float volume = 1f)
            {
                if (!loaded) { clip = LoadWav(file); loaded = true; }
                if (clip == null) return;
                if (src == null)
                {
                    src = host.AddComponent<AudioSource>();
                    src.playOnAwake = false;
                    src.spatialBlend = 0f;
                }
                if (Mixer != null) src.outputAudioMixerGroup = Mixer;
                src.clip = clip;
                src.volume = volume;
                src.Play();
            }

            public void Stop()
            {
                if (src != null && src.isPlaying) src.Stop();
            }
        }
    }
}
