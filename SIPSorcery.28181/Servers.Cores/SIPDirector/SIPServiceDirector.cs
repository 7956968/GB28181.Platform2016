﻿using Logger4Net;
using SIPSorcery.GB28181.Net;
using SIPSorcery.GB28181.Servers.SIPMessage;
using SIPSorcery.GB28181.Sys;
using SIPSorcery.GB28181.Sys.XML;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SIPSorcery.GB28181.Servers
{
    public class SIPServiceDirector : ISIPServiceDirector
    {
        private static ILog logger = AppState.logger;
        private ISipMessageCore _sipCoreMessageService;
        private Dictionary<string, Catalog> _Catalogs = new Dictionary<string, Catalog>();
        public Dictionary<string, Catalog> Catalogs => _Catalogs;
        private Queue<NotifyCatalog.Item> _NotifyCatalogItem = new Queue<NotifyCatalog.Item>();
        public Queue<NotifyCatalog.Item> NotifyCatalogItem => _NotifyCatalogItem; 
        private Dictionary<string, DeviceStatus> _DeviceStatuses = new Dictionary<string, DeviceStatus>();
        public Dictionary<string, DeviceStatus> DeviceStatuses => _DeviceStatuses;
        private Dictionary<string, RecordInfo> _RecordInfoes = new Dictionary<string, RecordInfo>();
        public Dictionary<string, RecordInfo> RecordInfoes => _RecordInfoes;

        public SIPServiceDirector(ISipMessageCore sipCoreMessageService)
        {
            _sipCoreMessageService = sipCoreMessageService;
            _sipCoreMessageService.OnCatalogReceived += _sipCoreMessageService_OnCatalogReceived;
            _sipCoreMessageService.OnNotifyCatalogReceived += _sipCoreMessageService_OnNotifyCatalogReceived;
            //_sipCoreMessageService.OnAlarmReceived += _sipCoreMessageService_OnAlarmReceived;
            _sipCoreMessageService.OnDeviceStatusReceived += _sipCoreMessageService_OnDeviceStatusReceived;
            _sipCoreMessageService.OnRecordInfoReceived += _sipCoreMessageService_OnRecordInfoReceived;
        }

        //#region 报警通知
        //private void _sipCoreMessageService_OnAlarmReceived(Alarm obj)
        //{
        //    var msg = "DeviceID:" + obj.DeviceID +
        //      "\r\nSN:" + obj.SN +
        //      "\r\nCmdType:" + obj.CmdType +
        //      "\r\nAlarmPriority:" + obj.AlarmPriority +
        //      "\r\nAlarmMethod:" + obj.AlarmMethod +
        //      "\r\nAlarmTime:" + obj.AlarmTime +
        //      "\r\nAlarmDescription:" + obj.AlarmDescription;
        //    new Action(() =>
        //    {
        //        _sipCoreMessageService.NodeMonitorService[obj.DeviceID].AlarmResponse(obj);
        //    }).Invoke();
        //}
        //#endregion

        #region 实时视频流
        public ISIPMonitorCore GetTargetMonitorService(string gbid)
        {
            if (_sipCoreMessageService == null)
            {
                throw new NullReferenceException("instance not exist!");
            }
            if (_sipCoreMessageService.NodeMonitorService.ContainsKey(gbid))
            {
                return _sipCoreMessageService.NodeMonitorService[gbid];
            }
            return null;
        }
        /// <summary>
        /// make real Request
        /// </summary>
        /// <param name="gbid"></param>
        /// <param name="mediaPort"></param>
        /// <param name="receiveIP"></param>
        /// <returns></returns>
        async public Task<Tuple<string, int, ProtocolType>> MakeVideoRequest(string gbid, int[] mediaPort, string receiveIP)
        {
            logger.Debug("Make video request started.");
            var target = GetTargetMonitorService(gbid);
            if (target == null)
            {
                return null;
            }
            var taskResult = await Task.Factory.StartNew(() =>
            {
                var cSeq = target.RealVideoReq(mediaPort, receiveIP, true);
                var result = target.WaitRequestResult();
                return result;
            });
            var ipaddress = _sipCoreMessageService.GetReceiveIP(taskResult.Item2.Body);
            var port = _sipCoreMessageService.GetReceivePort(taskResult.Item2.Body, SDPMediaTypesEnum.video);
            return Tuple.Create(ipaddress, port, ProtocolType.Udp);
        }
        /// <summary>
        /// stop real Request
        /// </summary>
        /// <param name="gbid"></param>
        /// <returns></returns>
        async public Task<Tuple<string, int, ProtocolType>> Stop(string gbid)
        {
            var target = GetTargetMonitorService(gbid);

            if (target == null)
            {
                return null;
            }
            target.ByeVideoReq();
            logger.Debug("Make video request stopped.");
            return null;

            //var taskResult = await Task.Factory.StartNew(() =>
            //{
            //    //stop
            //    target.ByeVideoReq();
            //    var result = target.WaitRequestResult();
            //    return result;
            //});
            //var ipaddress = _sipCoreMessageService.GetReceiveIP(taskResult.Item2.Body);
            //var port = _sipCoreMessageService.GetReceivePort(taskResult.Item2.Body, SDPMediaTypesEnum.video);
            //return Tuple.Create(ipaddress, port, ProtocolType.Udp);
        }
        #endregion

        #region 设备目录
        private void _sipCoreMessageService_OnCatalogReceived(Catalog obj)
        {
            if (!Catalogs.ContainsKey(obj.DeviceID))
            {
                Catalogs.Add(obj.DeviceID, obj);
            }
            else
            {
                Catalogs.Remove(obj.DeviceID);
                Catalogs.Add(obj.DeviceID, obj);
            }
        }
        /// <summary>
        /// Device Catalog Query
        /// </summary>
        /// <param name="deviceId"></param>
        public void DeviceCatalogQuery(string deviceId)
        {
            logger.Debug("Device Catalog Query started.");
            _sipCoreMessageService.DeviceCatalogQuery(deviceId);
        }
        /// <summary>
        /// Device Catalog Subscribe
        /// </summary>
        /// <param name="deviceId"></param>
        public void DeviceCatalogSubscribe(string deviceId)
        {
            logger.Debug("Device Catalog Subscribe started.");
            _sipCoreMessageService.DeviceCatalogSubscribe(deviceId);
        }

        /// <summary>
        /// Notify Catalog Received
        /// </summary>
        /// <param name="obj"></param>
        private void _sipCoreMessageService_OnNotifyCatalogReceived(NotifyCatalog obj)
        {
            if (obj.DeviceList == null)
            {
                return;
            }
            new Action(() =>
            {
                foreach (var item in obj.DeviceList.Items)
                {
                    NotifyCatalogItem.Enqueue(item);
                }
            }).BeginInvoke(null, null);
        }
        #endregion

        #region 设备转动
        /// <summary>
        /// PTZ Control
        /// </summary>
        /// <param name="ptzCommand"></param>
        /// <param name="speed"></param>
        /// <param name="deviceid"></param>
        public void PtzControl(SIPMonitor.PTZCommand ptzCommand, int speed, string deviceid)
        {
            _sipCoreMessageService.PtzControl(ptzCommand, speed, deviceid);
        }
        #endregion
        #region 设备状态
        private void _sipCoreMessageService_OnDeviceStatusReceived(SIP.SIPEndPoint arg1, DeviceStatus arg2)
        {
            if (!DeviceStatuses.ContainsKey(arg2.DeviceID))
            {
                DeviceStatuses.Add(arg2.DeviceID, arg2);
            }
            else
            {
                DeviceStatuses.Remove(arg2.DeviceID);
                DeviceStatuses.Add(arg2.DeviceID, arg2);
            }
        }
        /// <summary>
        /// Device Status Query
        /// </summary>
        /// <param name="deviceid"></param>
        public void DeviceStateQuery(string deviceid)
        {
            _sipCoreMessageService.DeviceStateQuery(deviceid);
        }
        #endregion
        #region 录像点播
        private void _sipCoreMessageService_OnRecordInfoReceived(RecordInfo obj)
        {
            if (!RecordInfoes.ContainsKey(obj.DeviceID))
            {
                RecordInfoes.Add(obj.DeviceID, obj);
            }
            else
            {
                RecordInfoes.Remove(obj.DeviceID);
                RecordInfoes.Add(obj.DeviceID, obj);
            }
        }
        /// <summary>
        /// History Record File Query
        /// </summary>
        /// <param name="deviceId"></param>
        /// <param name="startTime"></param>
        /// <param name="endTime"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        public int RecordFileQuery(string deviceId, DateTime startTime, DateTime endTime, string type)
        {
            return _sipCoreMessageService.RecordFileQuery(deviceId, startTime, endTime, type);
        }
        #endregion
    }
}
