﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenTKImageViewer.ImageContext
{
    public delegate void ChangedFormularHandler(object sender, EventArgs e);
    
    public class ImageFormula
    {
        public string Original { get; private set; } = "I0";
        public string Converted { get; private set; } = "GetTexture0()";

        public event ChangedFormularHandler Changed;

        /// <summary>
        /// tries to apply the given formula. Throws an exception if the syntax is invalid
        /// </summary>
        /// <param name="formula"></param>
        /// <param name="numImages"></param>
        public void ApplyFormula(string formula, int numImages)
        {
            if (formula.Equals(Original))
                return;

            var eq = new Equation.Equation(formula, numImages);
            Converted = eq.GetOpenGlExpression();
            Original = formula;
            OnChanged();
        }

        protected virtual void OnChanged()
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}