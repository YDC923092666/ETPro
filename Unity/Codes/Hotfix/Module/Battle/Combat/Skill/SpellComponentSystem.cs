﻿using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System;
namespace ET
{
    [Timer(TimerType.PlayNextSkillStep)]
    [FriendClass(typeof(SpellComponent))]
    public class PlayNextSkillStep: ATimer<SpellComponent>
    {
        public override void Run(SpellComponent self)
        {
            try
            {
                self.PlayNextSkillStep(self.NextSkillStep);
            }
            catch (Exception e)
            {
                Log.Error($"move timer error: {self.Id}\n{e}");
            }
        }
    }
    [ObjectSystem]
    public class SpellComponentAwakeSystem : AwakeSystem<SpellComponent>
    {
        public override void Awake(SpellComponent self)
        {
            self.CurSkillConfigId = 0;
        }
    }
    [ObjectSystem]
    public class SpellComponentDestroySystem : DestroySystem<SpellComponent>
    {
        public override void Destroy(SpellComponent self)
        {
            self.Interrupt(true);
        }
    }
    [FriendClass(typeof(SpellComponent))]
    [FriendClass(typeof(SkillAbility))]
    [FriendClass(typeof(CombatUnitComponent))]
    public static class SpellComponentSystem
    {
        /// <summary>
        /// 当前技能
        /// </summary>
        public static SkillAbility GetSkill(this SpellComponent self)
        {
            if (self.GetParent<CombatUnitComponent>().TryGetSkillAbility(self.CurSkillConfigId, out var res))
            {
                return res;
            }
            return null;
        } 
        /// <summary>
        /// 设置是否可施法
        /// </summary>
        /// <param name="self"></param>
        /// <param name="enable"></param>
        public static void SetEnable(this SpellComponent self, bool enable)
        {
            self.Enable = enable;
            if(!enable)
                self.Interrupt(true);
        }
        /// <summary>
        /// 打断
        /// </summary>
        /// <param name="self"></param>
        /// <param name="force">强制打断(多用于死亡)</param>
        public static void Interrupt(this SpellComponent self,bool force = false)
        {
            if (self.CurSkillConfigId != 0)
            {
                var curStep = self.Para.GetCurStepPara();
                if (force||curStep.CanInterrupt)
                {
                    SkillWatcherComponent.Instance.Run(SkillStepType.Interrupt, self.Para);
                    self.CurSkillConfigId = 0;
                    self.Para.Dispose();
                    self.Para = null;
                    TimerComponent.Instance.Remove(ref self.TimerId);
                }
            }
        }

        /// <summary>
        /// 是否可打断
        /// </summary>
        /// <param name="self"></param>
        /// <returns></returns>
        public static bool CanInterrupt(this SpellComponent self)
        {
            if (self.CurSkillConfigId != 0)
            {
                var curStep = self.Para.GetCurStepPara();
                return curStep.CanInterrupt;
            }

            return true;
        }
        /// <summary>
        /// 结束
        /// </summary>
        /// <param name="self"></param>
        private static void OnSkillPlayOver(this SpellComponent self)
        {
            if(self.CurSkillConfigId!=0) 
                self.GetSkill().LastSpellOverTime = TimeHelper.ServerNow();
            self.CurSkillConfigId = 0;
            self.Para.Dispose();
            self.Para = null;
        }
        /// <summary>
        /// 释放对目标技能
        /// </summary>
        /// <param name="self"></param>
        /// <param name="spellSkill"></param>
        /// <param name="targetEntity"></param>
        public static void SpellWithTarget(this SpellComponent self, SkillAbility spellSkill, CombatUnitComponent targetEntity)
        {
            if (!self.Enable) return;
            if (self.CurSkillConfigId != 0)
                return;
            if(!spellSkill.CanUse())return;

            self.CurSkillConfigId = spellSkill.ConfigId;
            var nowpos = self.GetParent<CombatUnitComponent>().unit.Position;
            var nowpos2 = targetEntity.unit.Position;
            if (Vector2.Distance(new Vector2(nowpos.x, nowpos.z), new Vector2(nowpos2.x, nowpos2.z)) >
                spellSkill.SkillConfig.PreviewRange[0])
            {
                return;
            }
            self.Para = SkillPara.Create();
            self.Para.From = self.GetParent<CombatUnitComponent>();
            self.Para.Ability = spellSkill;
            self.Para.To = targetEntity;

            self.GetSkill().LastSpellTime = TimeHelper.ServerNow();
            self.PlayNextSkillStep(0);
        }
        /// <summary>
        /// 释放对点技能
        /// </summary>
        /// <param name="self"></param>
        /// <param name="spellSkill"></param>
        /// <param name="point"></param>
        public static void SpellWithPoint(this SpellComponent self,SkillAbility spellSkill, Vector3 point)
        {
            if (!self.Enable) return;
            if (self.CurSkillConfigId != 0)
                return;
            if(!spellSkill.CanUse())return;
            self.CurSkillConfigId = spellSkill.ConfigId;
            var nowpos = self.GetParent<CombatUnitComponent>().unit.Position;
            if (Vector2.Distance(new Vector2(nowpos.x, nowpos.z), new Vector2(point.x, point.z)) >
                spellSkill.SkillConfig.PreviewRange[0])
            {
                var dir =new Vector3(point.x - nowpos.x,0, point.z - nowpos.z).normalized;
                point = nowpos + dir * spellSkill.SkillConfig.PreviewRange[0];
            }
            self.Para = SkillPara.Create();
            self.Para.Position = point;
            self.Para.From = self.GetParent<CombatUnitComponent>();
            self.Para.Ability = spellSkill;

            self.GetSkill().LastSpellTime = TimeHelper.ServerNow();
            self.PlayNextSkillStep(0);
        }
        /// <summary>
        /// 释放方向技能
        /// </summary>
        /// <param name="self"></param>
        /// <param name="spellSkill"></param>
        /// <param name="point"></param>
        public static void SpellWithDirect(this SpellComponent self,SkillAbility spellSkill, Vector3 point)
        {
            if (!self.Enable) return;
            if (self.CurSkillConfigId != 0)
                return;
            if(!spellSkill.CanUse())return;
            self.CurSkillConfigId = spellSkill.ConfigId;
            var nowpos = self.GetParent<CombatUnitComponent>().unit.Position;
            point = new Vector3(point.x, nowpos.y, point.z);
            var Rotation = Quaternion.LookRotation(point - nowpos,Vector3.up);
            
            self.Para = SkillPara.Create();
            self.Para.Position = point;
            self.Para.Rotation = Rotation;
            self.Para.From = self.GetParent<CombatUnitComponent>();
            self.Para.Ability = spellSkill;

            self.GetSkill().LastSpellTime = TimeHelper.ServerNow();
            self.PlayNextSkillStep(0);
        }
        /// <summary>
        /// 触发下一个技能触发点
        /// </summary>
        /// <param name="self"></param>
        /// <param name="index"></param>
        public static void PlayNextSkillStep(this SpellComponent self,int index)
        {
            do
            {
                if (self.CurSkillConfigId==0||self.GetSkill().StepType==null||index >=self.GetSkill().StepType.Count)
                {
                    self.OnSkillPlayOver();
                    return;
                }

                var id = self.GetSkill().StepType[index];
                self.Para.SetParaStep(index);
                SkillWatcherComponent.Instance.Run(id, self.Para);
                index++;
            } 
            while (self.Para.StepPara[index-1].Interval<=0);
            self.NextSkillStep = index;
            self.TimerId = TimerComponent.Instance.NewOnceTimer(
                TimeHelper.ServerNow() + self.Para.StepPara[index-1].Interval, TimerType.PlayNextSkillStep, self);
        }

        static void SetParaStep(this SkillPara para,int index)
        {
            if(para.Ability==null) return;
            
            var stepPara = new SkillStepPara();
            stepPara.Index = index;
            stepPara.Paras = null;
            stepPara.Interval = 0;
            if (para.Ability.Paras != null && index < para.Ability.Paras.Count)
            {
                stepPara.Paras = para.Ability.Paras[index];
            }
            if (para.Ability.TimeLine != null && index < para.Ability.TimeLine.Count)
            {
                stepPara.Interval = para.Ability.TimeLine[index];
            }
            if (para.Ability.CanInterrupt != null && index < para.Ability.CanInterrupt.Count)
            {
                stepPara.CanInterrupt = para.Ability.CanInterrupt[index];
            }
            stepPara.Count = 0;
            
            para.CurIndex = index;
            para.StepPara.Add(stepPara);
        }
        static SkillStepPara GetCurStepPara(this SkillPara para)
        {
            if(para.Ability==null||para.CurIndex>=para.StepPara.Count) return null;
            
            return para.StepPara[para.CurIndex];
        }
        static SkillStepPara GetStepPara(this SkillPara para,int index)
        {
            if(para.Ability==null||index>=para.StepPara.Count) return null;
            
            return para.StepPara[index];
        }
    }
}