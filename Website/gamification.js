// Gamification System - Points, Streaks, Badges

class GamificationSystem {
    constructor() {
        this.points = 0;
        this.streak = 0;
        this.badges = [];
        this.lastRepTime = 0;
        this.dailyReps = 0;
        this.lastDate = new Date().toDateString();
    }
    
    // Add points when rep is completed
    addPoints(accuracy) {
        // Points based on accuracy (1-10 points)
        let pointsEarned = Math.floor(accuracy / 10);
        if (pointsEarned < 1) pointsEarned = 1;
        
        // Bonus for high accuracy
        if (accuracy >= 95) pointsEarned += 5;
        if (accuracy >= 85) pointsEarned += 2;
        
        this.points += pointsEarned;
        this.updateStreak();
        this.checkBadges();
        
        return pointsEarned;
    }
    
    // Update streak
    updateStreak() {
        const now = Date.now();
        const timeSinceLastRep = (now - this.lastRepTime) / 1000;
        
        if (timeSinceLastRep < 30) {
            // Rapid reps - maintain streak
            this.streak += 1;
        } else if (timeSinceLastRep < 300) {
            // Within 5 minutes - keep streak
            this.streak += 1;
        } else {
            // Too long - reset streak
            this.streak = 1;
        }
        
        this.lastRepTime = now;
        
        // Check daily reset
        const today = new Date().toDateString();
        if (today !== this.lastDate) {
            this.dailyReps = 0;
            this.lastDate = today;
        }
        this.dailyReps += 1;
    }
    
    // Check and award badges
    checkBadges() {
        const newBadges = [];
        
        // Rep milestones
        if (this.dailyReps >= 10 && !this.badges.includes('10_reps')) {
            this.badges.push('10_reps');
            newBadges.push({name: '10 Reps Club', icon: '🏅', description: 'Completed 10 reps in a day'});
        }
        if (this.dailyReps >= 50 && !this.badges.includes('50_reps')) {
            this.badges.push('50_reps');
            newBadges.push({name: '50 Reps Master', icon: '🏆', description: 'Completed 50 reps in a day'});
        }
        
        // Streak badges
        if (this.streak >= 10 && !this.badges.includes('streak_10')) {
            this.badges.push('streak_10');
            newBadges.push({name: 'On Fire!', icon: '🔥', description: '10 rep streak'});
        }
        if (this.streak >= 25 && !this.badges.includes('streak_25')) {
            this.badges.push('streak_25');
            newBadges.push({name: 'Unstoppable!', icon: '⚡', description: '25 rep streak'});
        }
        
        // Accuracy badges
        const recentAccuracies = this.accuracyHistory || [];
        const avgAccuracy = recentAccuracies.reduce((a,b) => a+b, 0) / recentAccuracies.length;
        if (avgAccuracy >= 90 && !this.badges.includes('perfect_form')) {
            this.badges.push('perfect_form');
            newBadges.push({name: 'Perfect Form', icon: '🎯', description: '90%+ average accuracy'});
        }
        
        // Points milestones
        if (this.points >= 100 && !this.badges.includes('100_points')) {
            this.badges.push('100_points');
            newBadges.push({name: '100 Points', icon: '⭐', description: 'Earned 100 points'});
        }
        if (this.points >= 500 && !this.badges.includes('500_points')) {
            this.badges.push('500_points');
            newBadges.push({name: '500 Points', icon: '💎', description: 'Earned 500 points'});
        }
        
        return newBadges;
    }
    
    // Get stats for display
    getStats() {
        return {
            points: this.points,
            streak: this.streak,
            dailyReps: this.dailyReps,
            badges: this.badges.length
        };
    }
    
    // Save accuracy for badge calculation
    setAccuracyHistory(history) {
        this.accuracyHistory = history;
    }
}

// Create global instance
const gameSystem = new GamificationSystem();