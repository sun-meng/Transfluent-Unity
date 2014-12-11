﻿#define STORE_API_TOKEN_LOCALLY
//handles interaction with core code, allowing hte editor window to focus on presentation.  Also has the nice side effect of avoiding issues in editor files (massive bugginess with optional arguments, etc)
//seperatoion of logic, and

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace transfluent.editor
{
	public class TransfluentEditorWindowMediator
	{
		const string apiTokenEditorKey = "EDITOR_API_TOKEN";
		private LanguageList allLanguagesSupported;
		private TransfluentLanguage currentLanguage; //put this in a view state?
		private readonly IKeyStore _keyStore = new EditorKeyStore();
		private readonly InjectionContext context;

		public TransfluentEditorWindowMediator()
		{
			context = new InjectionContext();
			context.addMapping<ICredentialProvider>(new EditorKeyCredentialProvider());
			context.addMapping<IWebService>(new SyncronousEditorWebRequest());
		}

		public KeyValuePair<string, string> getUserNamePassword()
		{
			var provider = context.manualGetMapping<ICredentialProvider>();
			return new KeyValuePair<string, string>(provider.username, provider.password);
		}

		public void setUsernamePassword(string username, string password)
		{
			var provider = context.manualGetMapping<ICredentialProvider>();
			provider.save(username, password);
		}

		public bool authIsDone()
		{
			var authToken = getCurrentAuthToken();
			if(allLanguagesSupported == null)
			{
				getLanguageList();
			}
			return !string.IsNullOrEmpty(authToken) && allLanguagesSupported != null;
		}

		public string getCurrentAuthToken()
		{
			string retVal = null;
			try
			{
				retVal = context.manualGetMapping<string>(NamedInjections.API_TOKEN);
			}
			catch (KeyNotFoundException)
			{

			} //this is ok... I don't want to rewrite manualGetMapping
#if STORE_API_TOKEN_LOCALLY && UNITY_EDITOR
			if(!string.IsNullOrEmpty(retVal))
			{
				EditorPrefs.SetString(apiTokenEditorKey, retVal);
				return retVal;
			}
			var savedToken = EditorPrefs.GetString(apiTokenEditorKey);
			if(string.IsNullOrEmpty(savedToken)) return retVal;
			context.addNamedMapping<string>(NamedInjections.API_TOKEN, savedToken);
			retVal = savedToken;
#endif
			return retVal;
		}

		public bool doAuth(string username, string password)
		{
			if(string.IsNullOrEmpty(getCurrentAuthToken()))
			{
				var login = new Login(username, password);
				string authToken;
				try
				{
					authToken = login.Parse(makeCall(login)).token;
				}
				catch (CallException e)
				{
#if STORE_API_TOKEN_LOCALLY && UNITY_EDITOR
					//if we got a call error, just clear the key in case it has to do with that
					EditorPrefs.SetString(apiTokenEditorKey,"");
#endif
#if UNITY_EDITOR
					EditorUtility.DisplayDialog("Error",
						"Could not login, please re-enter credentials that you got on transfluent.com website", "OK");
#endif
					Debug.LogError("error getting login auth token:" + e.Message);
					throw;
					//previously tried to gracefully handle this, but I want to make sure that login errors are well-understood in any circumstance
				}
				
				context.addNamedMapping<string>(NamedInjections.API_TOKEN, authToken);
			}
			if(allLanguagesSupported == null)
			{
				getLanguageList();
				if(allLanguagesSupported == null) return false;
			}

			return true;
		}

		public LanguageList getLanguageList()
		{
			allLanguagesSupported = ResourceLoadFacade.getLanguageList();
			if(allLanguagesSupported == null)
			{
				requestAllLanguagesInEditorSynchronous();
				if(allLanguagesSupported != null)
				{
					//allLanguagesSupported,
					var languageListSO = ResourceCreator.CreateSO<LanguageListSO>("LanguageList");
					languageListSO.list = allLanguagesSupported;
					//do I need to set this dirty?
				}
			}

			return allLanguagesSupported;
		}

		[Conditional("UNITY_EDITOR")]
		public void requestAllLanguagesInEditorSynchronous()
		{
			try
			{
				var languageRequest = new RequestAllLanguages();
				allLanguagesSupported = languageRequest.Parse(makeCall(languageRequest));
			}
			catch (CallException e)
			{
				Debug.LogError("error getting all languages:" + e.Message);
			}
		}

		public bool doAuth()
		{
			var usernamePassword = getUserNamePassword();
			return doAuth(usernamePassword.Key, usernamePassword.Value);
		}

		public List<string> getAllLanguageNames()
		{
			if(getLanguageList() == null)
				throw new Exception("Cannot login");
			var languageCodes = new List<string>();
			allLanguagesSupported.languages.ForEach((TransfluentLanguage lang) => { languageCodes.Add(lang.code); });
			return languageCodes;
		}

		public List<string> getAllLanguageCodes()
		{
			if(getLanguageList() == null)
				throw new Exception("Cannot login");
			var languageCodes = new List<string>();
			allLanguagesSupported.languages.ForEach((TransfluentLanguage lang) => { languageCodes.Add(lang.code); });
			return languageCodes;
		}

		public void setCurrentLanugageCode(string languageCode)
		{
			var knownCodes = getAllLanguageCodes();
			if(!knownCodes.Contains(languageCode)) throw new Exception("Tried to set language to an unknown language code");

			currentLanguage = allLanguagesSupported.getLangaugeByCode(languageCode);
		}

		public void invalidateAuth(bool wipeDatastore = false)
		{
			context.removeNamedMapping<string>(NamedInjections.API_TOKEN);
			allLanguagesSupported = null;
			if(wipeDatastore)
				context.manualGetMapping<ICredentialProvider>().save(null, null);
		}

		public string GetText(string textKey, string groupKey = null)
		{
			if(currentLanguage == null) throw new Exception("Must set current language first!");
			var getText = new GetTextKey(textKey, group_id: groupKey, languageID: currentLanguage.id);
			return getText.Parse(makeCall(getText));
		}

		private string makeCall(ITransfluentParameters call)
		{
			if(!string.IsNullOrEmpty(getCurrentAuthToken()))
			{
				context.setMappings(call);
				call.getParameters.Add("token", getCurrentAuthToken());
					//TODO: find best way to handle auth token.  construction param,injection or manual setting
			}

			var service = context.manualGetMapping<IWebService>();

			return service.request(call).text;
		}

		public void SaveGroupToServer(GameTranslationSet.GroupOfTranslations groupOfTranslations, TransfluentLanguage language)
		{
			var saveText = new SaveSetOfKeys(language.id, groupOfTranslations.getDictionaryCopy(), groupOfTranslations.groupid);
			try
			{
				makeCall(saveText);
			}
			catch (CallException exception)
			{
				Debug.LogError("error making setText call:" + exception.Message);
			}
		}

		public void SetText(string textKey, string textValue, string groupKey = null)
		{
			if(currentLanguage == null) throw new Exception("Must set current language first!");
			var saveText = new SaveTextKey(textKey, text: textValue, group_id: groupKey, language: currentLanguage.id);
			try
			{
				makeCall(saveText);
			}
			catch (CallException exception)
			{
				Debug.LogError("error making setText call:" + exception.Message);
			}
		}

		public List<TransfluentTranslation> knownTextEntries(string groupid = null)
		{
			if(currentLanguage == null) throw new Exception("Must set current language first!");

			var getAllKeys = new GetAllExistingTranslationKeys(currentLanguage.id, groupid);

			var list = new List<TransfluentTranslation>();
			try
			{
				var dictionaryOfKeys = getAllKeys.Parse(makeCall(getAllKeys));
				foreach (var transfluentTranslation in dictionaryOfKeys)
				{
					list.Add(transfluentTranslation.Value);
				}
			}
			catch (ApplicatonLevelException errorcode)
			{
				//Debug.Log("App error:"+errorcode);
				if(errorcode.details.type != 400.ToString())
					throw;
			}
			return list;
		}

		public void setCurrentLanguage(TransfluentLanguage lang)
		{
			_keyStore.set("CURRENT_LANGUAGE", lang.code);
			currentLanguage = lang;
		}

		public void setCurrentLanguageFromLanguageCode(string languageCode)
		{
			_keyStore.set("CURRENT_LANGUAGE", languageCode);
			currentLanguage = allLanguagesSupported.getLangaugeByCode(languageCode);
		}

		public TransfluentLanguage GetCurrentLanguage()
		{
			return currentLanguage;
		}

		public void SetText(List<TransfluentTranslation> translations)
		{
			foreach (var translation in translations)
			{
				if(string.IsNullOrEmpty(translation.text_id)) continue;
				//don't send empty string as a group id
				if(string.IsNullOrEmpty(translation.group_id)) translation.group_id = null;
				SetText(translation.text_id, translation.text, translation.group_id);
			}
		}
	}
}