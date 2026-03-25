# FlourishWellness Web App

## Live Date Target:  1 January 2027

## To do:
 - Only facility executive directors can assign surveys to employees
 - Users may only view the survey assigned to them, no other survey data
 - Email notifications and alerts system
 

## Completed:
 - ✅ Add "Community Name: Year" to the Survey page
 - ✅ Completed surveys are boolean
 - ✅ Surveys can be edited during the current year
 - ✅ Notify user before leaving with unsaved changes
 - ✅ Change SurveyYear column to use actual year
 - ✅ Add survey ID for each Facility (we already do this with CommunityKey and SurveyYear)
 - ✅ One survey per facility per year
 - ✅ Audit trail for all responses
 - ✅ Times/dates are now based on CST throughout
 - ✅ Removed all the local admin code
 - ✅ Basic employee landing page contains Take Survey option only, same with header buttons
 - ✅ Survey sections should open collapsed
 - ✅ Bottom of survey:  Submit and Lock (with warnings about editing)
 - ✅ Only admins and facility directors can unlock a locked survey
 - ✅ Only admins can lock or unlock a survey
 - ✅ Full audit trail when locking or unlocking surveys
 - ✅ Results use completed surveys, arranged by section and question with percentages bar chart per question
 - ✅ On the Manage Locks page, show a percent complete (rounded) on all surveys as a simple bar graph with the percentage complete value centered in it
 - ✅ On the survey page, use the same type of collapse style as the manage locks/manage survey pages
 - ✅ On the survey results page, group by section and question, collapsible but not started collapsed, and in the style of the manage locks/manage surveys pages
 - ✅ On login with an account with multiple facilities, disable the Take Survey button until a facility is selected
 - ✅ Removed "Dashboard" buttons from accounts without access to it (Employee, Manager)
 - ✅ Fixed so no accounts other than admin accounts can access the /dashboard and /admin pages; all others have zero access at all to these pages

 ## Notes
From the dev document:
 - Audit created
 - Audit incomplete 30 days after creation
 = Action item due in 14 days
 - Action item overdue
 - Audit finalized
 - Mid-year review reminder

 Notifications shall be configurable by Admin.