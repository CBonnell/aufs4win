/* Copyright 2013 Corey Bonnell

   This file is part of Aufs4Win.

    Aufs4Win is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Aufs4Win is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Aufs4Win.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.ObjectModel;

namespace Cbonnell.Aufs4Win
{
    internal class KeyedCollectionImpl<TKey, TItem> : KeyedCollection<TKey, TItem>
    {
        private readonly Converter<TItem, TKey> converter;

        public KeyedCollectionImpl(Converter<TItem, TKey> converter)
        {
            if (converter == null)
            {
                throw new ArgumentNullException("converter");
            }
            this.converter = converter;
        }

        protected override TKey GetKeyForItem(TItem item)
        {
            return this.converter.Invoke(item);
        }
    }
}