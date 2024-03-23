using System;
using System.Collections.Generic;
using UniLab.Common;
using UniLab.UI;
using UnityEngine;

namespace UniLab.InputBlocking
{
    public class InputBlockingManager : SingletonMonoBehaviour<InputBlockingManager>
    {
        [SerializeField] private RayCastTarget _rayCast = null;
        private readonly List<ulong> _blockingIdList = new();
        private Action<int> _onChangedBlockingIdList;

        public void Initialize()
        {
            SetDonDestroyOnLoad();
            SetActiveBlocking(false);
            SetEvent();
        }

        private void SetEvent()
        {
            _onChangedBlockingIdList = count => SetActiveBlocking(count != 0);
        }

        private void SetActiveBlocking(bool isActive)
        {
            _rayCast.gameObject.SetActive(isActive);
        }

        public void Push(ulong id)
        {
            _blockingIdList.Add(id);
            _onChangedBlockingIdList.Invoke(_blockingIdList.Count);
        }

        public void Pop(ulong id)
        {
            _blockingIdList.Remove(id);
            _onChangedBlockingIdList.Invoke(_blockingIdList.Count);
        }
    }
}