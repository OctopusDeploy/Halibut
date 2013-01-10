// Copyright 2012-2013 Octopus Deploy Pty. Ltd.
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//    http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Halibut.Server.Dispatch
{
    /// <summary>
    /// This class builds an object to invoke a late-bound method, without using MethodInfo.Invoke and thus avoiding exceptions being wrapped 
    /// as target invocation exceptions.
    /// </summary>
    static class DelegateInvoker
    {
        public static IActionInvokerWrapper CreateInvoker(object target, MethodInfo method)
        {
            var parameterTypes = new List<Type>();
            parameterTypes.AddRange(method.GetParameters().Select(x => x.ParameterType));
            parameterTypes.Add(method.ReturnType);

            var invokerType = _invokerTypes.SingleOrDefault(x => x.GetGenericArguments().Length == parameterTypes.Count);
            if (invokerType == null)
                throw new ArgumentException(string.Format("Could not create an invoker for the method '{0}'. This type of method is not supported.", method));

            invokerType = invokerType.MakeGenericType(parameterTypes.ToArray());

            var invokerWrapperType = _invokerWrapperTypes.SingleOrDefault(x => x.GetGenericArguments().Length == parameterTypes.Count);
            if (invokerWrapperType == null)
                throw new ArgumentException(string.Format("Could not create an invoker for the method '{0}'. This type of method is not supported.", method));

            invokerWrapperType = invokerWrapperType.MakeGenericType(parameterTypes.ToArray());

            var invoker = Delegate.CreateDelegate(invokerType, target, method);
            var wrapper = Activator.CreateInstance(invokerWrapperType, invoker);
            return (IActionInvokerWrapper) wrapper;
        }

        #region Generated

        static readonly Type[] _invokerTypes = new[]
        {
            typeof (ActionInvoker<,,,,,,,,,,,,,,,,,,,,>),
            typeof (ActionInvoker<,,,,,,,,,,,,,,,,,,,>),
            typeof (ActionInvoker<,,,,,,,,,,,,,,,,,,>),
            typeof (ActionInvoker<,,,,,,,,,,,,,,,,,>),
            typeof (ActionInvoker<,,,,,,,,,,,,,,,,>),
            typeof (ActionInvoker<,,,,,,,,,,,,,,,>),
            typeof (ActionInvoker<,,,,,,,,,,,,,,>),
            typeof (ActionInvoker<,,,,,,,,,,,,,>),
            typeof (ActionInvoker<,,,,,,,,,,,,>),
            typeof (ActionInvoker<,,,,,,,,,,,>),
            typeof (ActionInvoker<,,,,,,,,,,>),
            typeof (ActionInvoker<,,,,,,,,,>),
            typeof (ActionInvoker<,,,,,,,,>),
            typeof (ActionInvoker<,,,,,,,>),
            typeof (ActionInvoker<,,,,,,>),
            typeof (ActionInvoker<,,,,,>),
            typeof (ActionInvoker<,,,,>),
            typeof (ActionInvoker<,,,>),
            typeof (ActionInvoker<,,>),
            typeof (ActionInvoker<,>),
            typeof (ActionInvoker<>),
        };

        static readonly Type[] _invokerWrapperTypes = new[]
        {
            typeof (ActionInvokerWrapper<,,,,,,,,,,,,,,,,,,,,>),
            typeof (ActionInvokerWrapper<,,,,,,,,,,,,,,,,,,,>),
            typeof (ActionInvokerWrapper<,,,,,,,,,,,,,,,,,,>),
            typeof (ActionInvokerWrapper<,,,,,,,,,,,,,,,,,>),
            typeof (ActionInvokerWrapper<,,,,,,,,,,,,,,,,>),
            typeof (ActionInvokerWrapper<,,,,,,,,,,,,,,,>),
            typeof (ActionInvokerWrapper<,,,,,,,,,,,,,,>),
            typeof (ActionInvokerWrapper<,,,,,,,,,,,,,>),
            typeof (ActionInvokerWrapper<,,,,,,,,,,,,>),
            typeof (ActionInvokerWrapper<,,,,,,,,,,,>),
            typeof (ActionInvokerWrapper<,,,,,,,,,,>),
            typeof (ActionInvokerWrapper<,,,,,,,,,>),
            typeof (ActionInvokerWrapper<,,,,,,,,>),
            typeof (ActionInvokerWrapper<,,,,,,,>),
            typeof (ActionInvokerWrapper<,,,,,,>),
            typeof (ActionInvokerWrapper<,,,,,>),
            typeof (ActionInvokerWrapper<,,,,>),
            typeof (ActionInvokerWrapper<,,,>),
            typeof (ActionInvokerWrapper<,,>),
            typeof (ActionInvokerWrapper<,>),
            typeof (ActionInvokerWrapper<>),
        };

        #region Nested type: ActionInvoker

        delegate TReturn ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TArg12, TArg13, TArg14, TArg15, TArg16, TArg17, TArg18, TArg19, TReturn>(
            TArg0 arg0, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6, TArg7 arg7, TArg8 arg8, TArg9 arg9, TArg10 arg10, TArg11 arg11, TArg12 arg12, TArg13 arg13, TArg14 arg14, TArg15 arg15, TArg16 arg16, TArg17 arg17, TArg18 arg18, TArg19 arg19);

        delegate TReturn ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TArg12, TArg13, TArg14, TArg15, TArg16, TArg17, TArg18, TReturn>(
            TArg0 arg0, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6, TArg7 arg7, TArg8 arg8, TArg9 arg9, TArg10 arg10, TArg11 arg11, TArg12 arg12, TArg13 arg13, TArg14 arg14, TArg15 arg15, TArg16 arg16, TArg17 arg17, TArg18 arg18);

        delegate TReturn ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TArg12, TArg13, TArg14, TArg15, TArg16, TArg17, TReturn>(
            TArg0 arg0, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6, TArg7 arg7, TArg8 arg8, TArg9 arg9, TArg10 arg10, TArg11 arg11, TArg12 arg12, TArg13 arg13, TArg14 arg14, TArg15 arg15, TArg16 arg16, TArg17 arg17);

        delegate TReturn ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TArg12, TArg13, TArg14, TArg15, TArg16, TReturn>(TArg0 arg0, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6, TArg7 arg7, TArg8 arg8, TArg9 arg9, TArg10 arg10, TArg11 arg11, TArg12 arg12, TArg13 arg13, TArg14 arg14, TArg15 arg15, TArg16 arg16);

        delegate TReturn ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TArg12, TArg13, TArg14, TArg15, TReturn>(TArg0 arg0, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6, TArg7 arg7, TArg8 arg8, TArg9 arg9, TArg10 arg10, TArg11 arg11, TArg12 arg12, TArg13 arg13, TArg14 arg14, TArg15 arg15);

        delegate TReturn ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TArg12, TArg13, TArg14, TReturn>(TArg0 arg0, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6, TArg7 arg7, TArg8 arg8, TArg9 arg9, TArg10 arg10, TArg11 arg11, TArg12 arg12, TArg13 arg13, TArg14 arg14);

        delegate TReturn ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TArg12, TArg13, TReturn>(TArg0 arg0, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6, TArg7 arg7, TArg8 arg8, TArg9 arg9, TArg10 arg10, TArg11 arg11, TArg12 arg12, TArg13 arg13);

        delegate TReturn ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TArg12, TReturn>(TArg0 arg0, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6, TArg7 arg7, TArg8 arg8, TArg9 arg9, TArg10 arg10, TArg11 arg11, TArg12 arg12);

        delegate TReturn ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TReturn>(TArg0 arg0, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6, TArg7 arg7, TArg8 arg8, TArg9 arg9, TArg10 arg10, TArg11 arg11);

        delegate TReturn ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TReturn>(TArg0 arg0, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6, TArg7 arg7, TArg8 arg8, TArg9 arg9, TArg10 arg10);

        delegate TReturn ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TReturn>(TArg0 arg0, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6, TArg7 arg7, TArg8 arg8, TArg9 arg9);

        delegate TReturn ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TReturn>(TArg0 arg0, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6, TArg7 arg7, TArg8 arg8);

        delegate TReturn ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TReturn>(TArg0 arg0, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6, TArg7 arg7);

        delegate TReturn ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TReturn>(TArg0 arg0, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5, TArg6 arg6);

        delegate TReturn ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TReturn>(TArg0 arg0, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4, TArg5 arg5);

        delegate TReturn ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TReturn>(TArg0 arg0, TArg1 arg1, TArg2 arg2, TArg3 arg3, TArg4 arg4);

        delegate TReturn ActionInvoker<TArg0, TArg1, TArg2, TArg3, TReturn>(TArg0 arg0, TArg1 arg1, TArg2 arg2, TArg3 arg3);

        delegate TReturn ActionInvoker<TArg0, TArg1, TArg2, TReturn>(TArg0 arg0, TArg1 arg1, TArg2 arg2);

        delegate TReturn ActionInvoker<TArg0, TArg1, TReturn>(TArg0 arg0, TArg1 arg1);

        delegate TReturn ActionInvoker<TArg0, TReturn>(TArg0 arg0);

        delegate TReturn ActionInvoker<TReturn>();

        #endregion

        #region Nested type: ActionInvokerWrapper

        class ActionInvokerWrapper<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TArg12, TArg13, TArg14, TArg15, TArg16, TArg17, TArg18, TArg19, TReturn> : IActionInvokerWrapper
        {
            readonly ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TArg12, TArg13, TArg14, TArg15, TArg16, TArg17, TArg18, TArg19, TReturn> _invoker;

            public ActionInvokerWrapper(ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TArg12, TArg13, TArg14, TArg15, TArg16, TArg17, TArg18, TArg19, TReturn> invoker)
            {
                _invoker = invoker;
            }

            public object Call(object[] args)
            {
                var arg0 = (TArg0) args[0];
                var arg1 = (TArg1) args[1];
                var arg2 = (TArg2) args[2];
                var arg3 = (TArg3) args[3];
                var arg4 = (TArg4) args[4];
                var arg5 = (TArg5) args[5];
                var arg6 = (TArg6) args[6];
                var arg7 = (TArg7) args[7];
                var arg8 = (TArg8) args[8];
                var arg9 = (TArg9) args[9];
                var arg10 = (TArg10) args[10];
                var arg11 = (TArg11) args[11];
                var arg12 = (TArg12) args[12];
                var arg13 = (TArg13) args[13];
                var arg14 = (TArg14) args[14];
                var arg15 = (TArg15) args[15];
                var arg16 = (TArg16) args[16];
                var arg17 = (TArg17) args[17];
                var arg18 = (TArg18) args[18];
                var arg19 = (TArg19) args[19];
                var result = _invoker(arg0, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18, arg19);
                return result;
            }
        }

        class ActionInvokerWrapper<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TArg12, TArg13, TArg14, TArg15, TArg16, TArg17, TArg18, TReturn> : IActionInvokerWrapper
        {
            readonly ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TArg12, TArg13, TArg14, TArg15, TArg16, TArg17, TArg18, TReturn> _invoker;

            public ActionInvokerWrapper(ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TArg12, TArg13, TArg14, TArg15, TArg16, TArg17, TArg18, TReturn> invoker)
            {
                _invoker = invoker;
            }

            public object Call(object[] args)
            {
                var arg0 = (TArg0) args[0];
                var arg1 = (TArg1) args[1];
                var arg2 = (TArg2) args[2];
                var arg3 = (TArg3) args[3];
                var arg4 = (TArg4) args[4];
                var arg5 = (TArg5) args[5];
                var arg6 = (TArg6) args[6];
                var arg7 = (TArg7) args[7];
                var arg8 = (TArg8) args[8];
                var arg9 = (TArg9) args[9];
                var arg10 = (TArg10) args[10];
                var arg11 = (TArg11) args[11];
                var arg12 = (TArg12) args[12];
                var arg13 = (TArg13) args[13];
                var arg14 = (TArg14) args[14];
                var arg15 = (TArg15) args[15];
                var arg16 = (TArg16) args[16];
                var arg17 = (TArg17) args[17];
                var arg18 = (TArg18) args[18];
                var result = _invoker(arg0, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17, arg18);
                return result;
            }
        }

        class ActionInvokerWrapper<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TArg12, TArg13, TArg14, TArg15, TArg16, TArg17, TReturn> : IActionInvokerWrapper
        {
            readonly ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TArg12, TArg13, TArg14, TArg15, TArg16, TArg17, TReturn> _invoker;

            public ActionInvokerWrapper(ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TArg12, TArg13, TArg14, TArg15, TArg16, TArg17, TReturn> invoker)
            {
                _invoker = invoker;
            }

            public object Call(object[] args)
            {
                var arg0 = (TArg0) args[0];
                var arg1 = (TArg1) args[1];
                var arg2 = (TArg2) args[2];
                var arg3 = (TArg3) args[3];
                var arg4 = (TArg4) args[4];
                var arg5 = (TArg5) args[5];
                var arg6 = (TArg6) args[6];
                var arg7 = (TArg7) args[7];
                var arg8 = (TArg8) args[8];
                var arg9 = (TArg9) args[9];
                var arg10 = (TArg10) args[10];
                var arg11 = (TArg11) args[11];
                var arg12 = (TArg12) args[12];
                var arg13 = (TArg13) args[13];
                var arg14 = (TArg14) args[14];
                var arg15 = (TArg15) args[15];
                var arg16 = (TArg16) args[16];
                var arg17 = (TArg17) args[17];
                var result = _invoker(arg0, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16, arg17);
                return result;
            }
        }

        class ActionInvokerWrapper<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TArg12, TArg13, TArg14, TArg15, TArg16, TReturn> : IActionInvokerWrapper
        {
            readonly ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TArg12, TArg13, TArg14, TArg15, TArg16, TReturn> _invoker;

            public ActionInvokerWrapper(ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TArg12, TArg13, TArg14, TArg15, TArg16, TReturn> invoker)
            {
                _invoker = invoker;
            }

            public object Call(object[] args)
            {
                var arg0 = (TArg0) args[0];
                var arg1 = (TArg1) args[1];
                var arg2 = (TArg2) args[2];
                var arg3 = (TArg3) args[3];
                var arg4 = (TArg4) args[4];
                var arg5 = (TArg5) args[5];
                var arg6 = (TArg6) args[6];
                var arg7 = (TArg7) args[7];
                var arg8 = (TArg8) args[8];
                var arg9 = (TArg9) args[9];
                var arg10 = (TArg10) args[10];
                var arg11 = (TArg11) args[11];
                var arg12 = (TArg12) args[12];
                var arg13 = (TArg13) args[13];
                var arg14 = (TArg14) args[14];
                var arg15 = (TArg15) args[15];
                var arg16 = (TArg16) args[16];
                var result = _invoker(arg0, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15, arg16);
                return result;
            }
        }

        class ActionInvokerWrapper<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TArg12, TArg13, TArg14, TArg15, TReturn> : IActionInvokerWrapper
        {
            readonly ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TArg12, TArg13, TArg14, TArg15, TReturn> _invoker;

            public ActionInvokerWrapper(ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TArg12, TArg13, TArg14, TArg15, TReturn> invoker)
            {
                _invoker = invoker;
            }

            public object Call(object[] args)
            {
                var arg0 = (TArg0) args[0];
                var arg1 = (TArg1) args[1];
                var arg2 = (TArg2) args[2];
                var arg3 = (TArg3) args[3];
                var arg4 = (TArg4) args[4];
                var arg5 = (TArg5) args[5];
                var arg6 = (TArg6) args[6];
                var arg7 = (TArg7) args[7];
                var arg8 = (TArg8) args[8];
                var arg9 = (TArg9) args[9];
                var arg10 = (TArg10) args[10];
                var arg11 = (TArg11) args[11];
                var arg12 = (TArg12) args[12];
                var arg13 = (TArg13) args[13];
                var arg14 = (TArg14) args[14];
                var arg15 = (TArg15) args[15];
                var result = _invoker(arg0, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14, arg15);
                return result;
            }
        }

        class ActionInvokerWrapper<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TArg12, TArg13, TArg14, TReturn> : IActionInvokerWrapper
        {
            readonly ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TArg12, TArg13, TArg14, TReturn> _invoker;

            public ActionInvokerWrapper(ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TArg12, TArg13, TArg14, TReturn> invoker)
            {
                _invoker = invoker;
            }

            public object Call(object[] args)
            {
                var arg0 = (TArg0) args[0];
                var arg1 = (TArg1) args[1];
                var arg2 = (TArg2) args[2];
                var arg3 = (TArg3) args[3];
                var arg4 = (TArg4) args[4];
                var arg5 = (TArg5) args[5];
                var arg6 = (TArg6) args[6];
                var arg7 = (TArg7) args[7];
                var arg8 = (TArg8) args[8];
                var arg9 = (TArg9) args[9];
                var arg10 = (TArg10) args[10];
                var arg11 = (TArg11) args[11];
                var arg12 = (TArg12) args[12];
                var arg13 = (TArg13) args[13];
                var arg14 = (TArg14) args[14];
                var result = _invoker(arg0, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13, arg14);
                return result;
            }
        }

        class ActionInvokerWrapper<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TArg12, TArg13, TReturn> : IActionInvokerWrapper
        {
            readonly ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TArg12, TArg13, TReturn> _invoker;

            public ActionInvokerWrapper(ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TArg12, TArg13, TReturn> invoker)
            {
                _invoker = invoker;
            }

            public object Call(object[] args)
            {
                var arg0 = (TArg0) args[0];
                var arg1 = (TArg1) args[1];
                var arg2 = (TArg2) args[2];
                var arg3 = (TArg3) args[3];
                var arg4 = (TArg4) args[4];
                var arg5 = (TArg5) args[5];
                var arg6 = (TArg6) args[6];
                var arg7 = (TArg7) args[7];
                var arg8 = (TArg8) args[8];
                var arg9 = (TArg9) args[9];
                var arg10 = (TArg10) args[10];
                var arg11 = (TArg11) args[11];
                var arg12 = (TArg12) args[12];
                var arg13 = (TArg13) args[13];
                var result = _invoker(arg0, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12, arg13);
                return result;
            }
        }

        class ActionInvokerWrapper<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TArg12, TReturn> : IActionInvokerWrapper
        {
            readonly ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TArg12, TReturn> _invoker;

            public ActionInvokerWrapper(ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TArg12, TReturn> invoker)
            {
                _invoker = invoker;
            }

            public object Call(object[] args)
            {
                var arg0 = (TArg0) args[0];
                var arg1 = (TArg1) args[1];
                var arg2 = (TArg2) args[2];
                var arg3 = (TArg3) args[3];
                var arg4 = (TArg4) args[4];
                var arg5 = (TArg5) args[5];
                var arg6 = (TArg6) args[6];
                var arg7 = (TArg7) args[7];
                var arg8 = (TArg8) args[8];
                var arg9 = (TArg9) args[9];
                var arg10 = (TArg10) args[10];
                var arg11 = (TArg11) args[11];
                var arg12 = (TArg12) args[12];
                var result = _invoker(arg0, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11, arg12);
                return result;
            }
        }

        class ActionInvokerWrapper<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TReturn> : IActionInvokerWrapper
        {
            readonly ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TReturn> _invoker;

            public ActionInvokerWrapper(ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TArg11, TReturn> invoker)
            {
                _invoker = invoker;
            }

            public object Call(object[] args)
            {
                var arg0 = (TArg0) args[0];
                var arg1 = (TArg1) args[1];
                var arg2 = (TArg2) args[2];
                var arg3 = (TArg3) args[3];
                var arg4 = (TArg4) args[4];
                var arg5 = (TArg5) args[5];
                var arg6 = (TArg6) args[6];
                var arg7 = (TArg7) args[7];
                var arg8 = (TArg8) args[8];
                var arg9 = (TArg9) args[9];
                var arg10 = (TArg10) args[10];
                var arg11 = (TArg11) args[11];
                var result = _invoker(arg0, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10, arg11);
                return result;
            }
        }

        class ActionInvokerWrapper<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TReturn> : IActionInvokerWrapper
        {
            readonly ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TReturn> _invoker;

            public ActionInvokerWrapper(ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TArg10, TReturn> invoker)
            {
                _invoker = invoker;
            }

            public object Call(object[] args)
            {
                var arg0 = (TArg0) args[0];
                var arg1 = (TArg1) args[1];
                var arg2 = (TArg2) args[2];
                var arg3 = (TArg3) args[3];
                var arg4 = (TArg4) args[4];
                var arg5 = (TArg5) args[5];
                var arg6 = (TArg6) args[6];
                var arg7 = (TArg7) args[7];
                var arg8 = (TArg8) args[8];
                var arg9 = (TArg9) args[9];
                var arg10 = (TArg10) args[10];
                var result = _invoker(arg0, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9, arg10);
                return result;
            }
        }

        class ActionInvokerWrapper<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TReturn> : IActionInvokerWrapper
        {
            readonly ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TReturn> _invoker;

            public ActionInvokerWrapper(ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TArg9, TReturn> invoker)
            {
                _invoker = invoker;
            }

            public object Call(object[] args)
            {
                var arg0 = (TArg0) args[0];
                var arg1 = (TArg1) args[1];
                var arg2 = (TArg2) args[2];
                var arg3 = (TArg3) args[3];
                var arg4 = (TArg4) args[4];
                var arg5 = (TArg5) args[5];
                var arg6 = (TArg6) args[6];
                var arg7 = (TArg7) args[7];
                var arg8 = (TArg8) args[8];
                var arg9 = (TArg9) args[9];
                var result = _invoker(arg0, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8, arg9);
                return result;
            }
        }

        class ActionInvokerWrapper<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TReturn> : IActionInvokerWrapper
        {
            readonly ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TReturn> _invoker;

            public ActionInvokerWrapper(ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TArg8, TReturn> invoker)
            {
                _invoker = invoker;
            }

            public object Call(object[] args)
            {
                var arg0 = (TArg0) args[0];
                var arg1 = (TArg1) args[1];
                var arg2 = (TArg2) args[2];
                var arg3 = (TArg3) args[3];
                var arg4 = (TArg4) args[4];
                var arg5 = (TArg5) args[5];
                var arg6 = (TArg6) args[6];
                var arg7 = (TArg7) args[7];
                var arg8 = (TArg8) args[8];
                var result = _invoker(arg0, arg1, arg2, arg3, arg4, arg5, arg6, arg7, arg8);
                return result;
            }
        }

        class ActionInvokerWrapper<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TReturn> : IActionInvokerWrapper
        {
            readonly ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TReturn> _invoker;

            public ActionInvokerWrapper(ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TArg7, TReturn> invoker)
            {
                _invoker = invoker;
            }

            public object Call(object[] args)
            {
                var arg0 = (TArg0) args[0];
                var arg1 = (TArg1) args[1];
                var arg2 = (TArg2) args[2];
                var arg3 = (TArg3) args[3];
                var arg4 = (TArg4) args[4];
                var arg5 = (TArg5) args[5];
                var arg6 = (TArg6) args[6];
                var arg7 = (TArg7) args[7];
                var result = _invoker(arg0, arg1, arg2, arg3, arg4, arg5, arg6, arg7);
                return result;
            }
        }

        class ActionInvokerWrapper<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TReturn> : IActionInvokerWrapper
        {
            readonly ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TReturn> _invoker;

            public ActionInvokerWrapper(ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TArg6, TReturn> invoker)
            {
                _invoker = invoker;
            }

            public object Call(object[] args)
            {
                var arg0 = (TArg0) args[0];
                var arg1 = (TArg1) args[1];
                var arg2 = (TArg2) args[2];
                var arg3 = (TArg3) args[3];
                var arg4 = (TArg4) args[4];
                var arg5 = (TArg5) args[5];
                var arg6 = (TArg6) args[6];
                var result = _invoker(arg0, arg1, arg2, arg3, arg4, arg5, arg6);
                return result;
            }
        }

        class ActionInvokerWrapper<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TReturn> : IActionInvokerWrapper
        {
            readonly ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TReturn> _invoker;

            public ActionInvokerWrapper(ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TArg5, TReturn> invoker)
            {
                _invoker = invoker;
            }

            public object Call(object[] args)
            {
                var arg0 = (TArg0) args[0];
                var arg1 = (TArg1) args[1];
                var arg2 = (TArg2) args[2];
                var arg3 = (TArg3) args[3];
                var arg4 = (TArg4) args[4];
                var arg5 = (TArg5) args[5];
                var result = _invoker(arg0, arg1, arg2, arg3, arg4, arg5);
                return result;
            }
        }

        class ActionInvokerWrapper<TArg0, TArg1, TArg2, TArg3, TArg4, TReturn> : IActionInvokerWrapper
        {
            readonly ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TReturn> _invoker;

            public ActionInvokerWrapper(ActionInvoker<TArg0, TArg1, TArg2, TArg3, TArg4, TReturn> invoker)
            {
                _invoker = invoker;
            }

            public object Call(object[] args)
            {
                var arg0 = (TArg0) args[0];
                var arg1 = (TArg1) args[1];
                var arg2 = (TArg2) args[2];
                var arg3 = (TArg3) args[3];
                var arg4 = (TArg4) args[4];
                var result = _invoker(arg0, arg1, arg2, arg3, arg4);
                return result;
            }
        }

        class ActionInvokerWrapper<TArg0, TArg1, TArg2, TArg3, TReturn> : IActionInvokerWrapper
        {
            readonly ActionInvoker<TArg0, TArg1, TArg2, TArg3, TReturn> _invoker;

            public ActionInvokerWrapper(ActionInvoker<TArg0, TArg1, TArg2, TArg3, TReturn> invoker)
            {
                _invoker = invoker;
            }

            public object Call(object[] args)
            {
                var arg0 = (TArg0) args[0];
                var arg1 = (TArg1) args[1];
                var arg2 = (TArg2) args[2];
                var arg3 = (TArg3) args[3];
                var result = _invoker(arg0, arg1, arg2, arg3);
                return result;
            }
        }

        class ActionInvokerWrapper<TArg0, TArg1, TArg2, TReturn> : IActionInvokerWrapper
        {
            readonly ActionInvoker<TArg0, TArg1, TArg2, TReturn> _invoker;

            public ActionInvokerWrapper(ActionInvoker<TArg0, TArg1, TArg2, TReturn> invoker)
            {
                _invoker = invoker;
            }

            public object Call(object[] args)
            {
                var arg0 = (TArg0) args[0];
                var arg1 = (TArg1) args[1];
                var arg2 = (TArg2) args[2];
                var result = _invoker(arg0, arg1, arg2);
                return result;
            }
        }

        class ActionInvokerWrapper<TArg0, TArg1, TReturn> : IActionInvokerWrapper
        {
            readonly ActionInvoker<TArg0, TArg1, TReturn> _invoker;

            public ActionInvokerWrapper(ActionInvoker<TArg0, TArg1, TReturn> invoker)
            {
                _invoker = invoker;
            }

            public object Call(object[] args)
            {
                var arg0 = (TArg0) args[0];
                var arg1 = (TArg1) args[1];
                var result = _invoker(arg0, arg1);
                return result;
            }
        }

        class ActionInvokerWrapper<TArg0, TReturn> : IActionInvokerWrapper
        {
            readonly ActionInvoker<TArg0, TReturn> _invoker;

            public ActionInvokerWrapper(ActionInvoker<TArg0, TReturn> invoker)
            {
                _invoker = invoker;
            }

            public object Call(object[] args)
            {
                var arg0 = (TArg0) args[0];
                var result = _invoker(arg0);
                return result;
            }
        }

        class ActionInvokerWrapper<TReturn> : IActionInvokerWrapper
        {
            readonly ActionInvoker<TReturn> _invoker;

            public ActionInvokerWrapper(ActionInvoker<TReturn> invoker)
            {
                _invoker = invoker;
            }

            public object Call(object[] args)
            {
                var result = _invoker();
                return result;
            }
        }

        #endregion

        #endregion

        #region Nested type: IActionInvokerWrapper

        internal interface IActionInvokerWrapper
        {
            object Call(object[] args);
        }

        #endregion
    }
}