using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Chatrooms.Hubs
{
    public class ChatHub : Hub
    {
        /// <summary>
        /// 默认随机昵称前缀
        /// </summary>
        private readonly string NickDefault = "Anonymous";
        /// <summary>
        /// 用户列表
        /// </summary>
        private static List<User> UserList = new List<User>();
        /// <summary>
        /// 房间列表
        /// </summary>
        private static List<Room> RoomList = new List<Room>();
        /// <summary>
        /// 返回给用户的房间列表项
        /// </summary>
        private class SimpleRoomInfo
        {
            public string ID;
            public string RoomTitle;
            public long UserCount;
            public bool IsLocked;
        };
        /// <summary>
        /// 检查用户列表中是否有重复昵称
        /// </summary>
        /// <param name="NickName">待检查昵称</param>
        /// <returns>true: 有重复, false: 无重复</returns>
        private bool HaveSameNickName(string NickName)
        {
            foreach (var user in UserList)
            {
                if (user.NickName == NickName)
                {
                    return true;
                }
            }
            return false;
        }
        /// <summary>
        /// 重载连接
        /// </summary>
        /// <returns></returns>
        public override async Task OnConnectedAsync()
        {
            Random rand = new Random();
            string NickName;
            do
            {
                NickName = NickDefault + rand.Next(1, 10000).ToString();
            } while (HaveSameNickName(NickName));
            UserList.Add(new User(Context.ConnectionId, NickName));
            await Clients.Caller.SendAsync("SetNickDefault", NickName);
            await UpdateRoomList();
            await base.OnConnectedAsync();
        }
        /// <summary>
        /// 重载断开连接
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public override async Task OnDisconnectedAsync(Exception e)
        {
            await QuitRoom();
            UserList.Remove(UserList.Where(user => user.ID == Context.ConnectionId).FirstOrDefault());
            await base.OnDisconnectedAsync(e);
        }
        /// <summary>
        /// 自定义昵称
        /// </summary>
        /// <param name="NewNick">新昵称</param>
        /// <returns></returns>
        public async Task SetNickName(string NewNick)
        {
            if (NewNick.Length == 0)
            {
                await Clients.Caller.SendAsync("SetNickErr", "昵称不能为空！");
            }
            else if (NewNick.Length > 20)
            {
                await Clients.Caller.SendAsync("SetNickErr", "昵称过长！");
            }
            else
            {
                if (HaveSameNickName(NewNick))
                {
                    await Clients.Caller.SendAsync("SetNickErr", "昵称重复！");
                }
                else
                {
                    User tuser = UserList.Where(user => user.ID == Context.ConnectionId).FirstOrDefault();
                    string OldName = tuser.NickName;
                    tuser.NickName = NewNick;
                    await Clients.Caller.SendAsync("SetNickOK", NewNick);
                    await Clients.Group(tuser.RoomID).SendAsync("RoomSysMsg", OldName + " 更改昵称为：" + NewNick);
                }
            }
        }
        /// <summary>
        /// 转发房间消息
        /// </summary>
        /// <param name="Message">消息文本</param>
        /// <returns></returns>
        public async Task RoomMsg(string Message)
        {
            User tuser = UserList.Where(user => user.ID == Context.ConnectionId).FirstOrDefault();
            if (Message.Length > 0 && tuser.RoomID != string.Empty)
            {
                await Clients.Group(tuser.RoomID).SendAsync("RoomMsg", tuser.NickName, Message);
            }
        }
        /// <summary>
        /// 更新房间列表
        /// </summary>
        /// <returns></returns>
        public async Task UpdateRoomList()
        {
            List<SimpleRoomInfo> tRoomList = new List<SimpleRoomInfo>();
            foreach (var room in RoomList)
            {
                tRoomList.Add(new SimpleRoomInfo
                {
                    ID = room.ID,
                    UserCount = room.UserCount,
                    RoomTitle = room.RoomTitle,
                    IsLocked = (room.RoomPwd != string.Empty)
                });
            }
            await Clients.All.SendAsync("RoomList", tRoomList);
        }
        /// <summary>
        /// 用户请求加入房间
        /// </summary>
        /// <param name="RoomID">房间ID</param>
        /// <param name="pwd">房间密码</param>
        /// <returns></returns>
        public async Task JoinRoom(string RoomID, string pwd)
        {
            if (RoomList.Exists(room => room.ID == RoomID))
            {
                User tuser = UserList.Where(user => user.ID == Context.ConnectionId).FirstOrDefault();
                Room troom = RoomList.Where(room => room.ID == RoomID).FirstOrDefault();
                if (tuser.RoomID == string.Empty)
                {
                    // 密码正确
                    if (troom.RoomPwd == pwd)
                    {
                        troom.AddUser();
                        tuser.JoinRoom(troom);
                        await Groups.AddToGroupAsync(Context.ConnectionId, troom.ID);
                        await Clients.Group(troom.ID).SendAsync("RoomSysMsg", "欢迎 " + tuser.NickName);
                        await Clients.Group(troom.ID).SendAsync("RoomInfo", troom.RoomTitle, troom.UserCount);
                        await UpdateRoomList();
                    }
                    // 密码错误
                    else
                    {
                        await Clients.Caller.SendAsync("SysMsg", "房间密码错误，拒绝加入！");
                    }
                }
            }
            else
            {
                await Clients.Caller.SendAsync("SysMsg", "房间无效，无法加入！");
            }
        }
        /// <summary>
        /// 用户退出房间
        /// </summary>
        /// <returns></returns>
        public async Task QuitRoom()
        {
            User tuser = UserList.Where(user => user.ID == Context.ConnectionId).FirstOrDefault();
            if (tuser.RoomID != string.Empty)
            {
                Room troom = RoomList.Where(room => room.ID == tuser.RoomID).FirstOrDefault();
                troom.DecUser();
                tuser.QuitRoom();
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, troom.ID);
                // 房间人数为0, 删除房间
                if (troom.UserCount == 0)
                {
                    RoomList.Remove(troom);
                }
                // 房间人数不为0
                else
                {
                    await Clients.Group(troom.ID).SendAsync("RoomSysMsg", tuser.NickName + " 离开");
                    await UpdateRoomInfo(troom.ID);
                }
                await UpdateRoomList();
            }
        }
        /// <summary>
        /// 创建房间
        /// </summary>
        /// <param name="Title">房间标题</param>
        /// <param name="pwd">房间密码</param>
        /// <returns></returns>
        public async Task CreateRoom(string Title, string pwd)
        {
            User tuser = UserList.Where(user => user.ID == Context.ConnectionId).FirstOrDefault();
            if (tuser.RoomID == string.Empty)
            {
                if (Title.Length == 0)
                {
                    await Clients.Caller.SendAsync("CreateRoomErr", "房间标题不能为空！");
                }
                else if (Title.Length > 30)
                {
                    await Clients.Caller.SendAsync("CreateRoomErr", "房间标题过长！");
                }
                else
                {
                    if (pwd.Length > 20)
                    {
                        await Clients.Caller.SendAsync("CreateRoomErr", "房间密码过长！");
                    }
                    else
                    {
                        RoomList.Add(new Room(Title, pwd.Length == 0 ? string.Empty : pwd, tuser));
                        await Groups.AddToGroupAsync(Context.ConnectionId, tuser.RoomID);
                        await Clients.Caller.SendAsync("CreateRoomOK");
                        await UpdateRoomInfo(tuser.RoomID);
                        await UpdateRoomList();
                    }
                }
            }
            else
            {
                await Clients.Caller.SendAsync("SysMsg", "已经在房间内，拒绝创建！");
            }
        }
        /// <summary>
        /// 更新房间信息
        /// </summary>
        /// <param name="RoomID">房间ID</param>
        /// <returns></returns>
        public async Task UpdateRoomInfo(string RoomID)
        {
            Room troom = RoomList.Where(room => room.ID == RoomID).FirstOrDefault();
            await Clients.Group(RoomID).SendAsync("RoomInfo", troom.RoomTitle, troom.UserCount);
        }
    }
}
