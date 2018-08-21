﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Network.Remote
{
    /// <summary>
    /// 末端
    /// </summary>
    public interface IRemoteEndPoint
    {
        /// <summary>
        /// 连接的目标地址
        /// </summary>
        IPEndPoint ConnectIPEndPoint { get; set; }
        /// <summary>
        /// 连接后重映射的地址
        /// </summary>
        EndPoint RemappedEndPoint { get; }
    }

    /// <summary>
    /// 可连接的
    /// </summary>
    public interface IConnectable : IRemoteEndPoint
    {
        /// <summary>
        /// 断开连接事件
        /// </summary>
        event Action<SocketError> OnDisConnect;
        /// <summary>
        /// <para>异常在底层Task过程中捕获，返回值null表示成功，调用者不必写try catch</para>
        /// </summary>
        /// <param name="endPoint"></param>
        /// <param name="retryCount">重试次数，失败会返回最后一次的异常</param>
        /// <returns></returns>
        Task<Exception> ConnectAsync(IPEndPoint endPoint, int retryCount = 0);
        /// <summary>
        /// 主动断开连接 不会触发OnDisConnect事件
        /// </summary>
        void Disconnect();
    }

    /// <summary>
    /// 发送任意对象，只要它能被MessageLUT解析。
    /// </summary>
    public interface ISendMessage
    {
        /// <summary>
        /// 发送消息，无阻塞立刻返回
        /// <para>调用方 无法了解发送情况</para>
        /// 序列化过程同步执行，方法返回表示序列化已结束，修改message内容不影响发送数据。
        /// </summary>
        /// <param name="message"></param>
        /// <remarks>序列化开销不大，放在调用线程执行比使用单独的序列化线程更好</remarks>
        void SendAsync(object message);
    }

    /// <summary>
    /// rpc完成时方法签名
    /// </summary>
    /// <param name="message"></param>
    /// <param name="exception"></param>
    public delegate void RpcCallback(object message, Exception exception);

    /// <summary>
    /// 更新Rpc结果，框架调用，协助处理Rpc封装
    /// 每个session大约每秒30个包，超时时间默认为30秒；
    /// </summary>
    public interface IRpcCallbackPool
    {
        /// <summary>
        /// Rpc超时毫秒数
        /// </summary>
        int RpcTimeOutMilliseconds { get; set; }
        (short rpcID, Task<(RpcResult result, Exception exception)> source) Regist<RpcResult>();
        (short rpcID, ILazyAwaitable<RpcResult> source) Regist<RpcResult>(Action<Exception> OnException);
        bool TryGetValue(short rpcID, out (DateTime startTime, RpcCallback rpcCallback) rpc);
        bool TryDequeue(short rpcID, out (DateTime startTime, RpcCallback rpcCallback) rpc);
        void Remove(short rpcID);
        bool TrySetResult(short rpcID, object msg);
        bool TrySetException(short rpcID, Exception exception);
    }

    /// <summary>
    /// RpcSend会自动开始Receive。
    /// <para></para>
    /// 为什么使用object 关键字而不是泛型？1.为了函数调用过程中更优雅。2.在序列化过程中，使用一次dynamic还原参数真实类型。
    /// <para>object导致值类型装箱是可以妥协的。</para>
    /// </summary>
    public interface IRpcSendMessage
    {
        /// <summary>
        /// 异步发送消息，封装Rpc过程。
        /// </summary>
        /// <typeparam name="RpcResult">期待的Rpc结果类型，如果收到返回类型，但是类型不匹配，返回null</typeparam>
        /// <param name="message">发送消息的类型需要序列化 具体实现使用查找表<see cref="MessageLUT"/> 中指定ID和序列化函数</param>
        /// <returns>需要检测空值</returns>
        /// <exception cref="NullReferenceException">返回值是空的</exception>
        /// <exception cref="TimeoutException">超时，等待指定时间内没有收到回复</exception>
        /// <exception cref="InvalidCastException">收到返回的消息，但类型不是<typeparamref name="RpcResult"/></exception>
        Task<(RpcResult result, Exception exception)> SendAsync<RpcResult>(object message);

    }

    /// <summary>
    /// RpcSend会自动开始Receive。
    /// <para></para>
    /// 为什么使用object 关键字而不是泛型？1.为了函数调用过程中更优雅。2.在序列化过程中，使用一次dynamic还原参数真实类型。
    /// <para>object导致值类型装箱是可以妥协的。</para>
    /// </summary>
    public interface ISafeAwaitSendMessage
    {
        /// <summary>
        /// 异步发送消息，封装Rpc过程
        /// 结果值是保证有值的，如果结果值为空或其他异常,触发异常回调函数，不会抛出异常，所以不用try catch。
        /// 异步方法的后续部分不会触发，所以后续部分可以省去空检查。
        /// <para>****千万注意，只有在RpcResult有返回值的情况下，后续异步方法才会执行*****</para>
        /// </summary>
        /// <typeparam name="RpcResult"></typeparam>
        /// <param name="message"></param>
        /// <param name="OnException">发生异常时的回调函数</param>
        /// <returns></returns>
        /// <exception cref="NullReferenceException">返回值是空的</exception>
        /// <exception cref="TimeoutException">超时，等待指定时间内没有收到回复</exception>
        /// <exception cref="InvalidCastException">收到返回的消息，但类型不是<typeparamref name="RpcResult"/></exception>
        /// <remarks>可能会有内存泄漏，参考具体实现。也许这个方法应该叫UnSafe。</remarks>
        ILazyAwaitable<RpcResult> SendAsyncSafeAwait<RpcResult>(object message, Action<Exception> OnException = null);
    }

    /// <summary>
    /// 可以广播发送
    /// </summary>
    public interface IBroadCastSend
    {
        /// <summary>
        /// 用于广播方式的发送
        /// </summary>
        /// <param name="msgBuffer"></param>
        /// <returns></returns>
        Task BroadCastSendAsync(ArraySegment<byte> msgBuffer);
    }

    /// <summary>
    /// 可以断线重连的
    /// </summary>
    public interface IReConnectable
    {
        /// <summary>
        /// 打开关闭断线重连
        /// </summary>
        bool IsReConnect { get; set; }

        /// <summary>
        /// 尝试重连的最大时间，超过时间触发断开连接(毫秒)
        /// </summary>
        int ReConnectTime { get; set; }

        /// <summary>
        /// 触发断线重连
        /// </summary>
        event Action<IReConnectable> PreReConnect;
        /// <summary>
        /// 断线重连成功。重连失败触发断开连接<see cref="IConnectable.OnDisConnect"/>
        /// </summary>
        event Action<IReConnectable> ReConnectSuccess;
    }

    /// <summary>
    /// 分流消息，区分是否为RPC
    /// </summary>
    public interface IShuntMessage
    {
        /// <summary>
        /// 分拣Rpc消息
        /// </summary>
        /// <param name="rpcID"></param>
        /// <param name="msg"></param>
        void ShuntMessage(short rpcID, object msg);
    }

    /// <summary>
    /// 接收消息
    /// </summary>
    public interface IReceiveMessage:IShuntMessage
    {
        /// <summary>
        /// 最后一次收到消息的时间
        /// </summary>
        DateTime LastReceiveTime { get; }
    }



    /// <summary>
    /// 应用网络层API封装
    /// </summary>
    public interface IRemote : IRemoteEndPoint, ISendMessage, IReceiveMessage,
        IConnectable, IRpcSendMessage, IBroadCastSend, IDisposable
    {
        /// <summary>
        /// 预留给用户使用的ID，（用户自己赋值ID，自己管理引用，框架不做处理）
        /// </summary>
        int UserToken { get; set; }
        /// <summary>
        /// 
        /// </summary>
        Socket Client { get; }
        /// <summary>
        /// 
        /// </summary>
        bool IsVaild { get; }

        /// <summary>
        /// 这个是框架全局分配的ID。进程唯一。
        /// </summary>
        int Guid { get; }
    }

    /// <summary>
    /// 应用网络层API封装
    /// </summary>
    public interface ISuperRemote:IRemote,ISafeAwaitSendMessage
    {

    }

    #region 转发

    /// <summary>
    /// 接入点/转发器/路由
    /// </summary>
    public interface IRouter
    {

    }

    #endregion

}