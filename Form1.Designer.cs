namespace Rapid_Downloader
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.panelCategories = new System.Windows.Forms.Panel();
            this.treeViewCategories = new System.Windows.Forms.TreeView();
            this.lblCategories = new System.Windows.Forms.Label();
            this.taskListView = new System.Windows.Forms.ListView();
            this.panelCategories.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelCategories
            // 
            this.panelCategories.BackColor = System.Drawing.SystemColors.Window;
            this.panelCategories.Controls.Add(this.treeViewCategories);
            this.panelCategories.Controls.Add(this.lblCategories);
            this.panelCategories.Dock = System.Windows.Forms.DockStyle.Left;
            this.panelCategories.Location = new System.Drawing.Point(0, 0);
            this.panelCategories.Name = "panelCategories";
            this.panelCategories.Size = new System.Drawing.Size(200, 450);
            this.panelCategories.TabIndex = 0;
            // 
            // treeViewCategories
            // 
            this.treeViewCategories.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.treeViewCategories.Dock = System.Windows.Forms.DockStyle.Fill;
            this.treeViewCategories.FullRowSelect = true;
            this.treeViewCategories.HideSelection = false;
            this.treeViewCategories.ItemHeight = 24;
            this.treeViewCategories.Location = new System.Drawing.Point(0, 30);
            this.treeViewCategories.Name = "treeViewCategories";
            this.treeViewCategories.ShowLines = true;
            this.treeViewCategories.ShowPlusMinus = true;
            this.treeViewCategories.ShowRootLines = true;
            this.treeViewCategories.Size = new System.Drawing.Size(200, 420);
            this.treeViewCategories.TabIndex = 1;
            // 
            // lblCategories
            // 
            this.lblCategories.BackColor = System.Drawing.SystemColors.ControlLight;
            this.lblCategories.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblCategories.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lblCategories.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64)))));
            this.lblCategories.Location = new System.Drawing.Point(0, 0);
            this.lblCategories.Name = "lblCategories";
            this.lblCategories.Padding = new System.Windows.Forms.Padding(10, 0, 0, 0);
            this.lblCategories.Size = new System.Drawing.Size(200, 30);
            this.lblCategories.TabIndex = 0;
            this.lblCategories.Text = "Categories";
            this.lblCategories.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // taskListView
            // 
            this.taskListView.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.taskListView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.taskListView.FullRowSelect = true;
            this.taskListView.GridLines = true;
            this.taskListView.Location = new System.Drawing.Point(200, 0);
            this.taskListView.Name = "taskListView";
            this.taskListView.Size = new System.Drawing.Size(600, 450);
            this.taskListView.TabIndex = 1;
            this.taskListView.UseCompatibleStateImageBehavior = false;
            this.taskListView.View = System.Windows.Forms.View.Details;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(850, 480);
            this.Controls.Add(this.taskListView);
            this.Controls.Add(this.panelCategories);
            this.Name = "Form1";
            this.Text = "Rapid Downloader";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.panelCategories.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.Panel panelCategories;
        private System.Windows.Forms.Label lblCategories;
        private System.Windows.Forms.TreeView treeViewCategories;
        private System.Windows.Forms.ListView taskListView;
    }
}