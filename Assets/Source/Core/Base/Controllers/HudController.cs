﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class HudController : MonoBehaviour {

    // Use this for initialization
    void Start () {
        GameController.Instance.onPhaseProgress += OnPhaseProgressImpl;
        GameController.Instance.onPhaseChanged += OnPhaseChangedImpl;
    }

    private void OnDestroy()
    {
        if(GameController.Instance)
        {
            GameController.Instance.onPhaseProgress -= OnPhaseProgressImpl;
            GameController.Instance.onPhaseChanged -= OnPhaseChangedImpl;
        }
    }

    private void OnPhaseProgressImpl(GameState currentMode, float timeLeft)
    {
        //TODO Implement timer somewhere
        //if(currentMode == GameState.Day)
        //{
        //    m_phaseText.color = Color.black;
        //    m_phaseCountdown.color = Color.black;
        //}
        //else
        //{
        //    m_phaseText.color = Color.white;
        //    m_phaseCountdown.color = Color.white;
        //}
        //
        //m_phaseText.text = currentMode.ToString();
        //m_phaseCountdown.text = timeLeft.ToString();
    }

    private void OnPhaseChangedImpl(GameState currentMode, float timeLeft)
    {
        if (currentMode == GameState.Night)
        {
            
        }
        else
        {
            
        }
    }

    // Update is called once per frame
    void Update () {
		
	}

    public void OnMinimapZoomOut()
    {
        if(MapCanvasController.Instance.radarDistance < 90)
        {
            MapCanvasController.Instance.radarDistance += 20;
        }
    }

    public void OnMinimapZoomIn()
    {
        if (MapCanvasController.Instance.radarDistance > 10)
        {
            MapCanvasController.Instance.radarDistance -= 20;
        }
    }
}
