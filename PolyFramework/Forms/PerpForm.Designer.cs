namespace PolyFramework
{
    partial class PerpForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.perpBackgroundWorker = new System.ComponentModel.BackgroundWorker();
            this.aRelaxProgressBar = new System.Windows.Forms.ProgressBar();
            this.aRelaxCheckBox = new System.Windows.Forms.CheckBox();
            this.aPerpCheckBox = new System.Windows.Forms.CheckBox();
            this.aRelaxStepsBox = new System.Windows.Forms.MaskedTextBox();
            this.aPerpStepsBox = new System.Windows.Forms.MaskedTextBox();
            this.aPerpAngleBox = new System.Windows.Forms.MaskedTextBox();
            this.aPerpProgressBar = new System.Windows.Forms.ProgressBar();
            this.aRunButton = new System.Windows.Forms.Button();
            this.aStopButton = new System.Windows.Forms.Button();
            this.aCloseButton = new System.Windows.Forms.Button();
            this.aLabelRelaxSteps = new System.Windows.Forms.Label();
            this.aPerpStepsLabel = new System.Windows.Forms.Label();
            this.aAngleLabel = new System.Windows.Forms.Label();
            this.aRelaxLabel = new System.Windows.Forms.Label();
            this.aPerpGeneralLabel = new System.Windows.Forms.Label();
            this.aPerpAngleLabel = new System.Windows.Forms.Label();
            this.aResetButton = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // perpBackgroundWorker
            // 
            this.perpBackgroundWorker.WorkerReportsProgress = true;
            this.perpBackgroundWorker.WorkerSupportsCancellation = true;
            // 
            // aRelaxProgressBar
            // 
            this.aRelaxProgressBar.BackColor = System.Drawing.SystemColors.Control;
            this.aRelaxProgressBar.Location = new System.Drawing.Point(12, 57);
            this.aRelaxProgressBar.Name = "aRelaxProgressBar";
            this.aRelaxProgressBar.Size = new System.Drawing.Size(237, 23);
            this.aRelaxProgressBar.TabIndex = 0;
            // 
            // aRelaxCheckBox
            // 
            this.aRelaxCheckBox.AutoSize = true;
            this.aRelaxCheckBox.Location = new System.Drawing.Point(12, 12);
            this.aRelaxCheckBox.Name = "aRelaxCheckBox";
            this.aRelaxCheckBox.Size = new System.Drawing.Size(99, 17);
            this.aRelaxCheckBox.TabIndex = 1;
            this.aRelaxCheckBox.Text = "Run Relaxation";
            this.aRelaxCheckBox.UseVisualStyleBackColor = true;
            // 
            // aPerpCheckBox
            // 
            this.aPerpCheckBox.AutoSize = true;
            this.aPerpCheckBox.Location = new System.Drawing.Point(12, 86);
            this.aPerpCheckBox.Name = "aPerpCheckBox";
            this.aPerpCheckBox.Size = new System.Drawing.Size(71, 17);
            this.aPerpCheckBox.TabIndex = 2;
            this.aPerpCheckBox.Text = "Run Perp";
            this.aPerpCheckBox.UseVisualStyleBackColor = true;
            // 
            // aRelaxStepsBox
            // 
            this.aRelaxStepsBox.Location = new System.Drawing.Point(50, 31);
            this.aRelaxStepsBox.Mask = "+0000";
            this.aRelaxStepsBox.Name = "aRelaxStepsBox";
            this.aRelaxStepsBox.Size = new System.Drawing.Size(43, 20);
            this.aRelaxStepsBox.TabIndex = 3;
            // 
            // aPerpStepsBox
            // 
            this.aPerpStepsBox.Location = new System.Drawing.Point(50, 108);
            this.aPerpStepsBox.Mask = "+0000";
            this.aPerpStepsBox.Name = "aPerpStepsBox";
            this.aPerpStepsBox.Size = new System.Drawing.Size(43, 20);
            this.aPerpStepsBox.TabIndex = 4;
            // 
            // aPerpAngleBox
            // 
            this.aPerpAngleBox.Location = new System.Drawing.Point(50, 134);
            this.aPerpAngleBox.Mask = "+00.00";
            this.aPerpAngleBox.Name = "aPerpAngleBox";
            this.aPerpAngleBox.Size = new System.Drawing.Size(43, 20);
            this.aPerpAngleBox.TabIndex = 5;
            // 
            // aPerpProgressBar
            // 
            this.aPerpProgressBar.Location = new System.Drawing.Point(12, 163);
            this.aPerpProgressBar.Name = "aPerpProgressBar";
            this.aPerpProgressBar.Size = new System.Drawing.Size(237, 23);
            this.aPerpProgressBar.TabIndex = 6;
            // 
            // aRunButton
            // 
            this.aRunButton.Location = new System.Drawing.Point(12, 192);
            this.aRunButton.Name = "aRunButton";
            this.aRunButton.Size = new System.Drawing.Size(66, 23);
            this.aRunButton.TabIndex = 7;
            this.aRunButton.Text = "Run";
            this.aRunButton.UseVisualStyleBackColor = true;
            // 
            // aStopButton
            // 
            this.aStopButton.Location = new System.Drawing.Point(84, 192);
            this.aStopButton.Name = "aStopButton";
            this.aStopButton.Size = new System.Drawing.Size(51, 23);
            this.aStopButton.TabIndex = 8;
            this.aStopButton.Text = "Stop";
            this.aStopButton.UseVisualStyleBackColor = true;
            // 
            // aCloseButton
            // 
            this.aCloseButton.Location = new System.Drawing.Point(197, 192);
            this.aCloseButton.Name = "aCloseButton";
            this.aCloseButton.Size = new System.Drawing.Size(53, 23);
            this.aCloseButton.TabIndex = 9;
            this.aCloseButton.Text = "Close";
            this.aCloseButton.UseVisualStyleBackColor = true;
            // 
            // aLabelRelaxSteps
            // 
            this.aLabelRelaxSteps.AutoSize = true;
            this.aLabelRelaxSteps.Location = new System.Drawing.Point(12, 34);
            this.aLabelRelaxSteps.Name = "aLabelRelaxSteps";
            this.aLabelRelaxSteps.Size = new System.Drawing.Size(34, 13);
            this.aLabelRelaxSteps.TabIndex = 11;
            this.aLabelRelaxSteps.Text = "Steps";
            // 
            // aPerpStepsLabel
            // 
            this.aPerpStepsLabel.AutoSize = true;
            this.aPerpStepsLabel.Location = new System.Drawing.Point(10, 111);
            this.aPerpStepsLabel.Name = "aPerpStepsLabel";
            this.aPerpStepsLabel.Size = new System.Drawing.Size(34, 13);
            this.aPerpStepsLabel.TabIndex = 12;
            this.aPerpStepsLabel.Text = "Steps";
            // 
            // aAngleLabel
            // 
            this.aAngleLabel.AutoSize = true;
            this.aAngleLabel.Location = new System.Drawing.Point(10, 137);
            this.aAngleLabel.Name = "aAngleLabel";
            this.aAngleLabel.Size = new System.Drawing.Size(34, 13);
            this.aAngleLabel.TabIndex = 14;
            this.aAngleLabel.Text = "Angle";
            // 
            // aRelaxLabel
            // 
            this.aRelaxLabel.AutoSize = true;
            this.aRelaxLabel.Location = new System.Drawing.Point(110, 34);
            this.aRelaxLabel.Name = "aRelaxLabel";
            this.aRelaxLabel.Size = new System.Drawing.Size(83, 13);
            this.aRelaxLabel.TabIndex = 15;
            this.aRelaxLabel.Text = "RelaxFeedBack";
            // 
            // aPerpGeneralLabel
            // 
            this.aPerpGeneralLabel.AutoSize = true;
            this.aPerpGeneralLabel.Location = new System.Drawing.Point(113, 110);
            this.aPerpGeneralLabel.Name = "aPerpGeneralLabel";
            this.aPerpGeneralLabel.Size = new System.Drawing.Size(78, 13);
            this.aPerpGeneralLabel.TabIndex = 16;
            this.aPerpGeneralLabel.Text = "PerpFeedBack";
            // 
            // aPerpAngleLabel
            // 
            this.aPerpAngleLabel.AutoSize = true;
            this.aPerpAngleLabel.Location = new System.Drawing.Point(113, 134);
            this.aPerpAngleLabel.Name = "aPerpAngleLabel";
            this.aPerpAngleLabel.Size = new System.Drawing.Size(105, 13);
            this.aPerpAngleLabel.TabIndex = 17;
            this.aPerpAngleLabel.Text = "PerpAngleFeedBack";
            // 
            // aResetButton
            // 
            this.aResetButton.Location = new System.Drawing.Point(141, 192);
            this.aResetButton.Name = "aResetButton";
            this.aResetButton.Size = new System.Drawing.Size(50, 23);
            this.aResetButton.TabIndex = 18;
            this.aResetButton.Text = "Reset";
            this.aResetButton.UseVisualStyleBackColor = true;
            // 
            // PerpForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(262, 227);
            this.Controls.Add(this.aResetButton);
            this.Controls.Add(this.aPerpAngleLabel);
            this.Controls.Add(this.aPerpGeneralLabel);
            this.Controls.Add(this.aRelaxLabel);
            this.Controls.Add(this.aAngleLabel);
            this.Controls.Add(this.aPerpStepsLabel);
            this.Controls.Add(this.aLabelRelaxSteps);
            this.Controls.Add(this.aCloseButton);
            this.Controls.Add(this.aStopButton);
            this.Controls.Add(this.aRunButton);
            this.Controls.Add(this.aPerpProgressBar);
            this.Controls.Add(this.aPerpAngleBox);
            this.Controls.Add(this.aPerpStepsBox);
            this.Controls.Add(this.aRelaxStepsBox);
            this.Controls.Add(this.aPerpCheckBox);
            this.Controls.Add(this.aRelaxCheckBox);
            this.Controls.Add(this.aRelaxProgressBar);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PerpForm";
            this.ShowIcon = false;
            this.Text = "Perpendicular";
            this.Load += new System.EventHandler(this.PerpForm_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.ComponentModel.BackgroundWorker perpBackgroundWorker;
        private System.Windows.Forms.ProgressBar aRelaxProgressBar;
        private System.Windows.Forms.CheckBox aRelaxCheckBox;
        private System.Windows.Forms.CheckBox aPerpCheckBox;
        private System.Windows.Forms.MaskedTextBox aRelaxStepsBox;
        private System.Windows.Forms.MaskedTextBox aPerpStepsBox;
        private System.Windows.Forms.MaskedTextBox aPerpAngleBox;
        private System.Windows.Forms.ProgressBar aPerpProgressBar;
        private System.Windows.Forms.Button aRunButton;
        private System.Windows.Forms.Button aStopButton;
        private System.Windows.Forms.Button aCloseButton;
        private System.Windows.Forms.Label aLabelRelaxSteps;
        private System.Windows.Forms.Label aPerpStepsLabel;
        private System.Windows.Forms.Label aAngleLabel;
        private System.Windows.Forms.Label aRelaxLabel;
        private System.Windows.Forms.Label aPerpGeneralLabel;
        private System.Windows.Forms.Label aPerpAngleLabel;
        private System.Windows.Forms.Button aResetButton;
    }
}