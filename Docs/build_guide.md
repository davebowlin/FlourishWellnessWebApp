# FlourishWellness Web App

## Live Date Target:  1 January 2027

## To do:
 - Only facility executive directors can assign surveys to employees
 - Users may only view the survey assigned to them, no other survey data
 - Email notifications and alerts system
 - ??? Ensure surveys are linked to the exec director of the facility

## Completed:
 - (DONE) Add "Community Name: Year" to the Survey page
 - (DONE) Completed surveys are boolean
 - (DONE) Surveys can be edited during the current year
 - (DONE) Notify user before leaving with unsaved changes
 - (DONE) Change SurveyYear column to use actual year
 - (DONE) Add survey ID for each Facility (we already do this with CommunityKey and SurveyYear)
 - (DONE) One survey per facility per year
 - (DONE) Audit trail for all responses
 - (DONE) Times/dates are now based on CST throughout
 - (DONE) Removed all the local admin code
 - (DONE) User landing page contains Take Survey option only, same with header buttons
 - (DONE) Survey sections should open collapsed
 - (DONE) Bottom of survey:  Submit and Lock (with warnings about editing)
 - (DONE) Only facility admin/director can unlock a locked survey
 - (DONE) Results should only use COMPLETED SURVEY data, arranged by section and question with percentages bar chart per question

 ## Notes
From the dev document:
 - Audit created
 - Audit incomplete 30 days after creation
 = Action item due in 14 days
 - Action item overdue
 - Audit finalized
 - Mid-year review reminder

 Notifications shall be configurable by Admin.