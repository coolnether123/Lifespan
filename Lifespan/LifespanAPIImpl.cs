using ModAPI.Core;
using ModAPI.Util;
using System.Collections.Generic;

namespace Lifespan
{
    /// <summary>
    /// Implementation of the public Lifespan API.
    /// </summary>
    internal class LifespanAPIImpl : ILifespanAPI
    {
        private readonly AgeTracker _ageTracker;
        private readonly ElderIllnessManager _illnessManager;
        private readonly DevelopmentGeneManager _devGeneManager;
        private LifespanConfig _config;
        private readonly IModLogger _log;
        private readonly IPluginContext _ctx;

        public LifespanAPIImpl(IPluginContext ctx, LifespanConfig config, AgeTracker ageTracker, ElderIllnessManager illnessManager, DevelopmentGeneManager devGeneManager)
        {
            _ctx = ctx;
            _config = config;
            _ageTracker = ageTracker;
            _illnessManager = illnessManager;
            _devGeneManager = devGeneManager;
            _log = ctx.Log;
        }

        public int GetCharacterAgeWeeks(FamilyMember member) => _ageTracker.GetAgeWeeks(member);
        public int GetCharacterAgeYears(FamilyMember member) => _ageTracker.GetAgeYears(member);
        public void SetCharacterAgeWeeks(FamilyMember member, int weeks) => _ageTracker.SetAgeWeeks(member, weeks);
        public void SetCharacterAgeWeeks(BaseCharacter character, int weeks) => _ageTracker.SetAgeWeeks(character, weeks);
        public bool IsElder(FamilyMember member) => _ageTracker.IsElder(member);
        public List<string> GetActiveIllnesses(FamilyMember member) => _illnessManager.GetActiveIllnesses(member);
        public void AddIllness(FamilyMember member, string illnessId) => _illnessManager.AddIllnessExternal(member, illnessId);
        public void RemoveIllness(FamilyMember member, string illnessId) => _illnessManager.RemoveIllnessExternal(member, illnessId);
        public LifespanConfig GetConfiguration() => _config;

        // Generic / NPC API Implementation
        public int GenerateAgeForNPC(BaseCharacter character, AgeContext context) => _ageTracker.GenerateAgeForContext(character, context);
        public int GetCharacterAgeWeeks(BaseCharacter character) => _ageTracker.GetAgeWeeks(character);
        
        public int IncrementNPCAge(BaseCharacter character, int weeks)
        {
            if (character == null) return 0;
            
            int current = _ageTracker.GetAgeWeeks(character);
            int newAge = current + weeks;
            
            _ageTracker.SetExternalCharacterAge(character.GetId(), newAge);
            OnCharacterAged?.Invoke(character, newAge);
            
            return newAge;
        }

        public event System.Action<BaseCharacter, int> OnCharacterAged;

        public void SetConfiguration(LifespanConfig config)
        {
            if (config != null)
            {
                config.ValidateAndClamp();
                _config = config;
                
                // Update ModSettings keys
                _ctx.Settings.SetInt("adultAgeYears", _config.adultAgeYears);
                _ctx.Settings.SetInt("elderAgeYears", _config.elderAgeYears);
                _ctx.Settings.SetInt("maxAgeYears", _config.maxAgeYears);
                
                _ctx.Settings.SetInt("initialChildAgeYears", _config.initialChildAgeYears);
                _ctx.Settings.SetInt("initialAdultAgeYears", _config.initialAdultAgeYears);
                
                _ctx.Settings.SetFloat("elderIllnessBaseChance", _config.elderIllnessBaseChance);
                _ctx.Settings.SetFloat("deathProbabilityMultiplier", _config.deathProbabilityMultiplier);
                
                _ctx.Settings.SetBool("enableDementia", _config.enableDementia);
                _ctx.Settings.SetBool("enableArthritis", _config.enableArthritis);
                _ctx.Settings.SetBool("enableHeartDisease", _config.enableHeartDisease);
                _ctx.Settings.SetBool("enableFrailty", _config.enableFrailty);
                _ctx.Settings.SetBool("enableRespiratory", _config.enableRespiratory);
                
                _ctx.Settings.SetInt("agingIntervalWeeks", _config.agingIntervalWeeks);
                _ctx.Settings.SetInt("weeksAgedPerInterval", _config.weeksAgedPerInterval);

                _ctx.Settings.SetFloat("dementiaIntModifier", _config.dementiaIntModifier);
                _ctx.Settings.SetFloat("arthritisSpeedModifier", _config.arthritisSpeedModifier);
                _ctx.Settings.SetFloat("frailtyStrModifier", _config.frailtyStrModifier);
                _ctx.Settings.SetFloat("heartDiseaseAttackChance", _config.heartDiseaseAttackChance);
                _ctx.Settings.SetFloat("heartDiseaseAttackChance", _config.heartDiseaseAttackChance);
                _ctx.Settings.SetFloat("heartAttackDamage", _config.heartAttackDamage);

                // Update DevGeneManager with new hot-reloaded values
                if (_devGeneManager != null) _devGeneManager.RefreshSettings(_config);

                // Save to user.json override
                _ctx.Settings.SaveUser();
                
                _log.Info("Configuration updated and saved via ModAPI Settings.");
            }
        }
    }
}
