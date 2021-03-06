﻿namespace Kermalis.VGMusicStudio.Core.NDS.DSE
{
    internal class Channel
    {
        public readonly byte Index;

        public Track Owner;
        public EnvelopeState State;
        public byte RootKey;
        public byte Key;
        public byte NoteVelocity;
        public sbyte Panpot; // Not necessary
        public ushort BaseTimer;
        public ushort Timer;
        public uint NoteLength;
        public byte Volume;

        private int _pos;
        private short _prevLeft;
        private short _prevRight;

        private int _envelopeTimeLeft;
        private int _volumeIncrement;
        private int _velocity; // From 0-0x3FFFFFFF ((128 << 23) - 1)
        private byte _targetVolume;

        private byte _attackVolume;
        private byte _attack;
        private byte _decay;
        private byte _sustain;
        private byte _hold;
        private byte _decay2;
        private byte _release;

        // PCM8, PCM16, ADPCM
        private SWD.SampleBlock _sample;
        // PCM8, PCM16
        private int _dataOffset;
        // ADPCM
        private ADPCMDecoder _adpcmDecoder;
        private short _adpcmLoopLastSample;
        private short _adpcmLoopStepIndex;

        public Channel(byte i)
        {
            Index = i;
        }

        public bool StartPCM(SWD localswd, SWD masterswd, byte voice, int key, uint noteLength)
        {
            SWD.IProgramInfo programInfo = localswd.Programs.ProgramInfos[voice];
            if (programInfo != null)
            {
                for (int i = 0; i < programInfo.SplitEntries.Length; i++)
                {
                    SWD.ISplitEntry split = programInfo.SplitEntries[i];
                    if (key >= split.LowKey && key <= split.HighKey)
                    {
                        _sample = masterswd.Samples[split.SampleId];
                        Key = (byte)key;
                        RootKey = split.SampleRootKey;
                        BaseTimer = (ushort)(NDS.Utils.ARM7_CLOCK / _sample.WavInfo.SampleRate);
                        if (_sample.WavInfo.SampleFormat == SampleFormat.ADPCM)
                        {
                            _adpcmDecoder = new ADPCMDecoder(_sample.Data);
                        }
                        //attackVolume = sample.WavInfo.AttackVolume == 0 ? split.AttackVolume : sample.WavInfo.AttackVolume;
                        //attack = sample.WavInfo.Attack == 0 ? split.Attack : sample.WavInfo.Attack;
                        //decay = sample.WavInfo.Decay == 0 ? split.Decay : sample.WavInfo.Decay;
                        //sustain = sample.WavInfo.Sustain == 0 ? split.Sustain : sample.WavInfo.Sustain;
                        //hold = sample.WavInfo.Hold == 0 ? split.Hold : sample.WavInfo.Hold;
                        //decay2 = sample.WavInfo.Decay2 == 0 ? split.Decay2 : sample.WavInfo.Decay2;
                        //release = sample.WavInfo.Release == 0 ? split.Release : sample.WavInfo.Release;
                        //attackVolume = split.AttackVolume == 0 ? sample.WavInfo.AttackVolume : split.AttackVolume;
                        //attack = split.Attack == 0 ? sample.WavInfo.Attack : split.Attack;
                        //decay = split.Decay == 0 ? sample.WavInfo.Decay : split.Decay;
                        //sustain = split.Sustain == 0 ? sample.WavInfo.Sustain : split.Sustain;
                        //hold = split.Hold == 0 ? sample.WavInfo.Hold : split.Hold;
                        //decay2 = split.Decay2 == 0 ? sample.WavInfo.Decay2 : split.Decay2;
                        //release = split.Release == 0 ? sample.WavInfo.Release : split.Release;
                        _attackVolume = split.AttackVolume == 0 ? _sample.WavInfo.AttackVolume == 0 ? (byte)0x7F : _sample.WavInfo.AttackVolume : split.AttackVolume;
                        _attack = split.Attack == 0 ? _sample.WavInfo.Attack == 0 ? (byte)0x7F : _sample.WavInfo.Attack : split.Attack;
                        _decay = split.Decay == 0 ? _sample.WavInfo.Decay == 0 ? (byte)0x7F : _sample.WavInfo.Decay : split.Decay;
                        _sustain = split.Sustain == 0 ? _sample.WavInfo.Sustain == 0 ? (byte)0x7F : _sample.WavInfo.Sustain : split.Sustain;
                        _hold = split.Hold == 0 ? _sample.WavInfo.Hold == 0 ? (byte)0x7F : _sample.WavInfo.Hold : split.Hold;
                        _decay2 = split.Decay2 == 0 ? _sample.WavInfo.Decay2 == 0 ? (byte)0x7F : _sample.WavInfo.Decay2 : split.Decay2;
                        _release = split.Release == 0 ? _sample.WavInfo.Release == 0 ? (byte)0x7F : _sample.WavInfo.Release : split.Release;
                        DetermineEnvelopeStartingPoint();
                        _pos = 0;
                        _prevLeft = _prevRight = 0;
                        NoteLength = noteLength;
                        return true;
                    }
                }
            }
            return false;
        }

        public void Stop()
        {
            if (Owner != null)
            {
                Owner.Channels.Remove(this);
            }
            Owner = null;
            Volume = 0;
        }

        private bool CMDB1___sub_2074CA0()
        {
            bool b = true;
            bool ge = _sample.WavInfo.EnvMult >= 0x7F;
            bool ee = _sample.WavInfo.EnvMult == 0x7F;
            if (_sample.WavInfo.EnvMult > 0x7F)
            {
                ge = _attackVolume >= 0x7F;
                ee = _attackVolume == 0x7F;
            }
            if (!ee & ge
                && _attack > 0x7F
                && _decay > 0x7F
                && _sustain > 0x7F
                && _hold > 0x7F
                && _decay2 > 0x7F
                && _release > 0x7F)
            {
                b = false;
            }
            return b;
        }
        private void DetermineEnvelopeStartingPoint()
        {
            State = EnvelopeState.Two; // This isn't actually placed in this func
            bool atLeastOneThingIsValid = CMDB1___sub_2074CA0(); // Neither is this
            if (atLeastOneThingIsValid)
            {
                if (_attack != 0)
                {
                    _velocity = _attackVolume << 23;
                    State = EnvelopeState.Hold;
                    UpdateEnvelopePlan(0x7F, _attack);
                }
                else
                {
                    _velocity = 0x7F << 23;
                    if (_hold != 0)
                    {
                        UpdateEnvelopePlan(0x7F, _hold);
                        State = EnvelopeState.Decay;
                    }
                    else if (_decay != 0)
                    {
                        UpdateEnvelopePlan(_sustain, _decay);
                        State = EnvelopeState.Decay2;
                    }
                    else
                    {
                        UpdateEnvelopePlan(0, _release);
                        State = EnvelopeState.Six;
                    }
                }
                // Unk1E = 1
            }
            else if (State != EnvelopeState.One) // What should it be?
            {
                State = EnvelopeState.Zero;
                _velocity = 0x7F << 23;
            }
        }
        public void SetEnvelopePhase7_2074ED8()
        {
            if (State != EnvelopeState.Zero)
            {
                UpdateEnvelopePlan(0, _release);
                State = EnvelopeState.Seven;
            }
        }
        public int StepEnvelope()
        {
            if (State > EnvelopeState.Two)
            {
                if (_envelopeTimeLeft != 0)
                {
                    _envelopeTimeLeft--;
                    _velocity += _volumeIncrement;
                    if (_velocity < 0)
                    {
                        _velocity = 0;
                    }
                    else if (_velocity > 0x3FFFFFFF)
                    {
                        _velocity = 0x3FFFFFFF;
                    }
                }
                else
                {
                    _velocity = _targetVolume << 23;
                    switch (State)
                    {
                        default: return _velocity >> 23; // case 8
                        case EnvelopeState.Hold:
                        {
                            if (_hold == 0)
                            {
                                goto LABEL_6;
                            }
                            else
                            {
                                UpdateEnvelopePlan(0x7F, _hold);
                                State = EnvelopeState.Decay;
                            }
                            break;
                        }
                        case EnvelopeState.Decay:
                        LABEL_6:
                            {
                                if (_decay == 0)
                                {
                                    _velocity = _sustain << 23;
                                    goto LABEL_9;
                                }
                                else
                                {
                                    UpdateEnvelopePlan(_sustain, _decay);
                                    State = EnvelopeState.Decay2;
                                }
                                break;
                            }
                        case EnvelopeState.Decay2:
                        LABEL_9:
                            {
                                if (_decay2 == 0)
                                {
                                    goto LABEL_11;
                                }
                                else
                                {
                                    UpdateEnvelopePlan(0, _decay2);
                                    State = EnvelopeState.Six;
                                }
                                break;
                            }
                        case EnvelopeState.Six:
                        LABEL_11:
                            {
                                UpdateEnvelopePlan(0, 0);
                                State = EnvelopeState.Two;
                                break;
                            }
                        case EnvelopeState.Seven:
                        {
                            State = EnvelopeState.Eight;
                            _velocity = 0;
                            _envelopeTimeLeft = 0;
                            break;
                        }
                    }
                }
            }
            return _velocity >> 23;
        }
        private void UpdateEnvelopePlan(byte targetVolume, int envelopeParam)
        {
            if (envelopeParam == 0x7F)
            {
                _volumeIncrement = 0;
                _envelopeTimeLeft = int.MaxValue;
            }
            else
            {
                _targetVolume = targetVolume;
                _envelopeTimeLeft = _sample.WavInfo.EnvMult == 0
                    ? Utils.Duration32[envelopeParam] * 1000 / 10000
                    : Utils.Duration16[envelopeParam] * _sample.WavInfo.EnvMult * 1000 / 10000;
                _volumeIncrement = _envelopeTimeLeft == 0 ? 0 : ((targetVolume << 23) - _velocity) / _envelopeTimeLeft;
            }
        }

        public void Process(out short left, out short right)
        {
            if (Timer != 0)
            {
                int numSamples = (_pos + 0x100) / Timer;
                _pos = (_pos + 0x100) % Timer;
                // prevLeft and prevRight are stored because numSamples can be 0.
                for (int i = 0; i < numSamples; i++)
                {
                    short samp;
                    switch (_sample.WavInfo.SampleFormat)
                    {
                        case SampleFormat.PCM8:
                        {
                            // If hit end
                            if (_dataOffset >= _sample.Data.Length)
                            {
                                if (_sample.WavInfo.Loop)
                                {
                                    _dataOffset = (int)(_sample.WavInfo.LoopStart * 4);
                                }
                                else
                                {
                                    left = right = _prevLeft = _prevRight = 0;
                                    Stop();
                                    return;
                                }
                            }
                            samp = (short)((sbyte)_sample.Data[_dataOffset++] << 8);
                            break;
                        }
                        case SampleFormat.PCM16:
                        {
                            // If hit end
                            if (_dataOffset >= _sample.Data.Length)
                            {
                                if (_sample.WavInfo.Loop)
                                {
                                    _dataOffset = (int)(_sample.WavInfo.LoopStart * 4);
                                }
                                else
                                {
                                    left = right = _prevLeft = _prevRight = 0;
                                    Stop();
                                    return;
                                }
                            }
                            samp = (short)(_sample.Data[_dataOffset++] | (_sample.Data[_dataOffset++] << 8));
                            break;
                        }
                        case SampleFormat.ADPCM:
                        {
                            // If just looped
                            if (_adpcmDecoder.DataOffset == _sample.WavInfo.LoopStart * 4 && !_adpcmDecoder.OnSecondNibble)
                            {
                                _adpcmLoopLastSample = _adpcmDecoder.LastSample;
                                _adpcmLoopStepIndex = _adpcmDecoder.StepIndex;
                            }
                            // If hit end
                            if (_adpcmDecoder.DataOffset >= _sample.Data.Length && !_adpcmDecoder.OnSecondNibble)
                            {
                                if (_sample.WavInfo.Loop)
                                {
                                    _adpcmDecoder.DataOffset = (int)(_sample.WavInfo.LoopStart * 4);
                                    _adpcmDecoder.StepIndex = _adpcmLoopStepIndex;
                                    _adpcmDecoder.LastSample = _adpcmLoopLastSample;
                                    _adpcmDecoder.OnSecondNibble = false;
                                }
                                else
                                {
                                    left = right = _prevLeft = _prevRight = 0;
                                    Stop();
                                    return;
                                }
                            }
                            samp = _adpcmDecoder.GetSample();
                            break;
                        }
                        default: samp = 0; break;
                    }
                    samp = (short)(samp * Volume / 0x7F);
                    _prevLeft = (short)(samp * (-Panpot + 0x40) / 0x80);
                    _prevRight = (short)(samp * (Panpot + 0x40) / 0x80);
                }
            }
            left = _prevLeft;
            right = _prevRight;
        }
    }
}
