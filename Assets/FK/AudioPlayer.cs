﻿using UnityEngine;

namespace Panty
{
    public interface IAudioPlayer : IModule
    {
        void PlayBgm(string name, float clipVolume = 1f);
        void PlaySound(string name, float clipVolume = 1f);
        void StopBgm();
        void PauseBgm();

        AudioSource GetSound(string name, float clipVolume = 1f);
        ValueBinder<float> BgmVolume { get; }
        ValueBinder<float> SoundVolume { get; }
    }
    public class AudioPlayer : AbsModule, IAudioPlayer
    {
        private class Sound
        {
            private float clipVolume;
            public AudioSource source;
            public Sound(AudioSource source)
            {
                this.source = source;
            }
            public Sound Set(float volume)
            {
                clipVolume = volume;
                return this;
            }
            public void Volume(float newValue)
            {
                source.volume = clipVolume * newValue;
            }
            public bool IsPlaying => source.loop || source.isPlaying;
        }

        private IResLoader mLoader;

        private AudioSource mBGM;
        private Fade fade;
        private static float bgmClipVolume;
        private GameObject mRoot;
        private PArray<Sound> mOpenList;
        private PArray<Sound> mCloseList;

        public ValueBinder<float> BgmVolume { get; } = 0.5f;
        public ValueBinder<float> SoundVolume { get; } = 0.5f;

        protected override void OnInit()
        {
            mOpenList = new PArray<Sound>(8);
            mCloseList = new PArray<Sound>(8);

            mLoader = this.Module<IResLoader>();

            fade = new Fade(0f, BgmVolume);
            BgmVolume.RegisterWithInitValue(OnBgmVolumeChanged);
            SoundVolume.RegisterWithInitValue(OnSoundVolumeChanged);
        }
        void IAudioPlayer.PlayBgm(string name, float clipVolume)
        {
            if (mBGM == null)
            {
                if (mRoot == null) InitRoot();
                mBGM = mRoot.AddComponent<AudioSource>();
                mBGM.loop = true;
                mBGM.volume = 0;
                MonoKit.OnUpdate += OnUpdate;
            }
            if (mBGM.clip == null || mBGM.clip.name == name) return;
            bgmClipVolume = clipVolume;
            ResKit.AsyncLoad<AudioClip>("Audios/Bgm/" + name, TryPlay);
        }
        void IAudioPlayer.StopBgm()
        {
            if (mBGM == null) return;
            if (mBGM.isPlaying)
            {
                fade.Out();
                fade.Set(mBGM.Stop);
            }
        }
        void IAudioPlayer.PauseBgm()
        {
            if (mBGM == null) return;
            if (mBGM.isPlaying)
            {
                fade.Out();
                fade.Set(mBGM.Pause);
            }
        }
        void IAudioPlayer.PlaySound(string name, float clipVolume)
        {
            TryGetSource(clipVolume, out var temp);
            var clip = mLoader.SyncLoad<AudioClip>("Sound/" + name);
            temp.clip = clip;
            temp.volume = clipVolume * SoundVolume;
            temp.loop = false;
            temp.Play();
        }
        AudioSource IAudioPlayer.GetSound(string name, float clipVolume)
        {
            TryGetSource(clipVolume, out var temp);
            var clip = mLoader.SyncLoad<AudioClip>("Sound/" + name);
            temp.clip = clip;
            temp.volume = clipVolume * SoundVolume;
            temp.loop = true;
            return temp;
        }
        private void TryGetSource(float clipVolume, out AudioSource source)
        {
            if (mCloseList.IsEmpty)
            {
                if (mRoot == null) InitRoot();
                int i = 0;
                while (i < mOpenList.Count)
                {
                    var sound = mOpenList[i];
                    if (sound.IsPlaying) i++;
                    else
                    {
                        mOpenList.RmvAt(i);
                        mCloseList.Push(sound);
                    }
                }
                if (mCloseList.IsEmpty)
                {
                    source = mRoot.AddComponent<AudioSource>();
                    mOpenList.Push(new Sound(source).Set(clipVolume));
                }
                else
                {
                    var sound = mCloseList.Pop();
                    source = sound.source;
                    mOpenList.Push(sound.Set(clipVolume));
                }
            }
            else
            {
                var sound = mCloseList.Pop();
                source = sound.source;
                mOpenList.Push(sound.Set(clipVolume));
            }
        }
        private void OnBgmVolumeChanged(float v)
        {
            if (mBGM == null) return;
            mBGM.volume = v * bgmClipVolume;
        }
        private void OnSoundVolumeChanged(float v)
        {
            foreach (var sound in mOpenList)
                sound.Volume(v);
        }
        private void OnUpdate()
        {
            if (fade.IsClose) return;
            fade.Update(Time.deltaTime);
            mBGM.volume = fade.Cur;
        }
        private void TryPlay(AudioClip clip)
        {
            if (mBGM.isPlaying)
            {
                fade.Out();
                fade.Set(() => Play(clip));
            }
            else Play(clip);
        }
        private void Play(AudioClip clip)
        {
            fade.In();
            fade.Set(null);
            fade.Max = BgmVolume * bgmClipVolume;
            mBGM.clip = clip;
            mBGM.Play();
        }
        // 初始化根节点
        private void InitRoot()
        {
            mRoot = new GameObject("AudioPool");
            GameObject.DontDestroyOnLoad(mRoot);
        }
    }
}