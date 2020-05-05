﻿using System;
using System.Collections.Generic;
using Microsoft.VisualStudio;
using System.Runtime.InteropServices;

namespace PeasyMotion
{
    // based on code from VsVim.
    /* VsVim
    Copyright 2012 Jared Parsons

    Licensed under the Apache License, Version 2.0 (the "License");
    you may not use this file except in compliance with the License.
    You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

    Unless required by applicable law or agreed to in writing, software
    distributed under the License is distributed on an "AS IS" BASIS,
    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
    See the License for the specific language governing permissions and
    limitations under the License.
    */

    public readonly struct Result
    {
        private readonly bool _isSuccess;
        private readonly int _hresult;

        public bool IsSuccess
        {
            get { return _isSuccess; }
        }

        public bool IsError
        {
            get { return !_isSuccess; }
        }

        public int HResult
        {
            get
            {
                if (!IsError)
                {
                    throw new InvalidOperationException();
                }
                return _hresult;
            }
        }

        private Result(int hresult)
        {
            _hresult = hresult;
            _isSuccess = ErrorHandler.Succeeded(hresult);
        }

        public static Result Error
        {
            get { return new Result(VSConstants.E_FAIL); }
        }

        public static Result Success
        {
            get { return new Result(VSConstants.S_OK); }
        }

        public static Result<T> CreateSuccess<T>(T value)
        {
            return new Result<T>(value);
        }

        public static Result<T> CreateSuccessNonNull<T>(T value)
            where T : class
        {
            if (value == null)
            {
                return Result.Error;
            }

            return new Result<T>(value);
        }

        public static Result CreateError(int value)
        {
            return new Result(value);
        }

        public static Result CreateError(Exception ex)
        {
            return CreateError(Marshal.GetHRForException(ex));
        }

        public static Result<T> CreateSuccessOrError<T>(T potentialValue, int hresult)
        {
            return ErrorHandler.Succeeded(hresult)
                ? CreateSuccess(potentialValue)
                : new Result<T>(hresult: hresult);
        }
    }

    public readonly struct Result<T>
    {
        private readonly bool _isSuccess;
        private readonly T _value;
        private readonly int _hresult;

        public bool IsSuccess
        {
            get { return _isSuccess; }
        }

        public bool IsError
        {
            get { return !_isSuccess; }
        }

        // TOOD: Get rid of this.  Make it a method that says throws
        public T Value
        {
            get
            {
                if (!IsSuccess)
                {
                    throw new InvalidOperationException();
                }

                return _value;
            }
        }

        public int HResult
        {
            get
            {
                if (IsSuccess)
                {
                    throw new InvalidOperationException();
                }

                return _hresult;
            }
        }

        public Result(T value)
        {
            _value = value;
            _isSuccess = true;
            _hresult = 0;
        }

        public Result(int hresult)
        {
            _hresult = hresult;
            _isSuccess = false;
            _value = default;
        }

        public T GetValueOrDefault(T defaultValue = default)
        {
            return IsSuccess ? Value : defaultValue;
        }

        public bool TryGetValue(out T value)
        {
            if (IsSuccess)
            {
                value = Value;
                return true;
            }

            value = default;
            return false;
        }

        public static implicit operator Result<T>(Result result)
        {
            return new Result<T>(hresult: result.HResult);
        }

        public static implicit operator Result<T>(T value)
        {
            return new Result<T>(value);
        }
    } 
}
