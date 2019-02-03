using System;
using System.Collections.Generic;
using DBZMOD.Extensions;
using DBZMOD.Util;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Enums;
using Terraria.ID;
using Terraria.ModLoader;
using ProjectileExtensions = DBZMOD.Extensions.ProjectileExtensions;

namespace DBZMOD.Projectiles
{
    // unabashedly stolen from blushie's laser example, and then customized WIP
	public abstract class BaseBeam : ModProjectile
    {
        // all beams tend to have a similar structure, there's a charge, a tail or "start", a beam (body) and a head (forwardmost point)
        // this is the structure that helps alleviate some of the logic burden by predefining the dimensions of each segment.
        protected Point 
            tailOrigin = new Point(14, 0),
            tailSize = new Point(46, 72),
            beamOrigin = new Point(14, 74),
            beamSize = new Point(46, 36),
            headOrigin = new Point(0, 112),
            headSize = new Point(74, 74);
        
        // this determines how long the max fade in for beam opacity takes to fully "phase in", at a rate of 1f per frame. (This is handled by the charge ball)
        public float beamFadeOutTime = 30f;

        // Bigger number = slower movement. For reference, 60f is pretty fast. 180f is pretty slow.
        public float rotationSlowness = 60f;

        // vector to reposition the beam tail down if it feels too low or too high on the character sprite
        public Vector2 offsetY = new Vector2(0, -14f);

        // the maximum travel distance the beam can go
        public float maxBeamDistance = 2000f;

        // the speed at which the beam head travels through space
        public float beamSpeed = 24f;

        // the type of dust to spawn when the beam is firing
        public int dustType = 169;

        // the frequency at which to spawn dust when the beam is firing
        public float dustFrequency = 0.6f;

        // how many particles per frame fire while firing the beam.
        public int fireParticleDensity = 6;

        // the frequency at which to spawn dust when the beam collides with something
        public float collisionDustFrequency = 1.0f;

        // how many particles per frame fire when the beam collides with something
        public int collisionParticleDensity = 8;

        // how many I-Frames your target receives when taking damage from the blast. Take care, this makes beams stupid strong.
        public int immunityFrameOverride = 5;

        // Flag for whether the beam segment is animated (meaning it has its own movement protocol), defaults to false.
        public bool isBeamSegmentAnimated = false;

        // The sound effect used by the projectile when firing the beam. (plays on initial fire only)
        public string beamSoundKey = "Sounds/BasicBeamFire";

        // The sound slot used by the projectile to kill the sounds it's making
        public KeyValuePair<uint, SoundEffectInstance> beamSoundSlotId;

        // I'm not sure this ever needs to be changed, but we can always change it later.
        //The distance charge particle from the player center
        public float TailHeldDistance
        {
            get
            {
                return (tailSize.Y / 2f) + 10f;
            }
        }

        // Beam can't be moved when rotating the mouse, it can only stay in its original position
        public bool isStationaryBeam = false;

        // Beam doesn't penetrate targets until they're dead (it doesn't penetrate at all, really)
        public bool isEntityColliding = false;
        
        // controls what sections of the beam segment we're drawing at any given point in time (assumes two or more beam segments tile correctly)
        private int _beamSegmentAnimation = 0;

        public Rectangle TailRectangle()
        {
            return new Rectangle(tailOrigin.X, tailOrigin.Y, tailSize.X, tailSize.Y);
        }
                
        public Rectangle BeamRectangle()
        {
            return new Rectangle(beamOrigin.X, beamOrigin.Y, beamSize.X, beamSize.Y);
        }

        // special handling for segment animation when a beam has animations in the central segment.
        public Rectangle BeamRectangleAnimatedSegment1()
        {
            return new Rectangle(beamOrigin.X, beamOrigin.Y + beamSize.Y - _beamSegmentAnimation, beamSize.X, _beamSegmentAnimation);
        }

        public Rectangle BeamRectangleAnimatedSegment2()
        {
            return new Rectangle(beamOrigin.X, beamOrigin.Y, beamSize.X, beamSize.Y - _beamSegmentAnimation);
        }

        public Rectangle HeadRectangle()
        {
            return new Rectangle(headOrigin.X, headOrigin.Y, headSize.X, headSize.Y);
        }

        // The actual distance is stored in the ai0 field
        // By making a property to handle this it makes our life easier, and the accessibility more readable
        public float Distance
        {
            get
            {
                return projectile.ai[0];
            }
            set
            {
                projectile.ai[0] = value;
                projectile.netUpdate = true;
            }
        }

        public float FiringTime
        {
            get
            {
                return projectile.ai[1];
            }
            set
            {
                projectile.ai[1] = value;
                projectile.netUpdate = true;
            }
        }

        public bool IsDetached
        {
            get
            {
                return projectile.localAI[0] > 0f;
            }
        }        

        public float DetachmentTimer
        {
            get
            {
                return projectile.localAI[0];
            }
            set
            {
                projectile.localAI[0] = value;
                projectile.netUpdate = true;
            }
        }
        
        public override void SetDefaults()
        {
            projectile.width = 10;
            projectile.height = 10;
            projectile.friendly = true;
            projectile.penetrate = -1;
            projectile.tileCollide = false;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Color lightColor)
        {            
            return false;
        }

        // this handles scaling the beam down by the wall distance
        private float wallDistanceScaling = 1.0f;

        public override void PostDraw(SpriteBatch spriteBatch, Color lightColor)
        {
            DrawLaser(spriteBatch, Main.projectileTexture[projectile.type], Color.White, projectile.scale * wallDistanceScaling);
        }

        // The core function of drawing a laser
        public void DrawLaser(SpriteBatch spriteBatch, Texture2D texture, Color color, float scale = 1f)
        {               
            // half pi subtracted from the rotation.
            float rotation = projectile.velocity.ToRotation() - 1.57f;
            Vector2 trueTailStart = TailStart() - TailHeldWallDistanceScaleOffset();
            Vector2 trueTailEnd = TailEnd() - (TailHeldWallDistanceScaleOffset() + TailRecessionWallDistanceScaleOffset());
            Vector2 trueBodyEnd = BodyEnd() - (TailHeldWallDistanceScaleOffset() + TailRecessionWallDistanceScaleOffset());

            // draw the beam tail
            spriteBatch.Draw(texture, trueTailStart - Main.screenPosition, TailRectangle(), color, rotation, new Vector2(tailSize.X * .5f, tailSize.Y * .5f), scale, 0, 0f);
                        
            // draw the body between the beam and its destination point. We do this in two sections if the beam is "animated"
            for (float i = 0f; i < Distance / scale; i += beamSize.Y)
            {
                Vector2 origin = trueTailEnd + i * scale * projectile.velocity;
                
                if (_beamSegmentAnimation > 0)
                {
                    spriteBatch.Draw(texture, origin - Main.screenPosition, BeamRectangleAnimatedSegment1(), color, rotation, new Vector2(beamSize.X * .5f, beamSize.Y * .5f), scale, 0, 0f);
                    spriteBatch.Draw(texture, origin + (_beamSegmentAnimation * projectile.velocity) - Main.screenPosition, BeamRectangleAnimatedSegment2(), color, rotation, new Vector2(beamSize.X * .5f, beamSize.Y * .5f), scale, 0, 0f);
                }
                else
                {
                    spriteBatch.Draw(texture, origin - Main.screenPosition, BeamRectangle(), color, rotation, new Vector2(beamSize.X * .5f, beamSize.Y * .5f), scale, 0, 0f);
                }
            }

            // draw the beam head
            spriteBatch.Draw(texture, trueBodyEnd - Main.screenPosition, HeadRectangle(), color, rotation, new Vector2(headSize.X * .5f, headSize.Y * .5f), scale, 0, 0f);
        }

        public Vector2 TailHeldWallDistanceScaleOffset()
        {
            return TailHeldOffset() * (1f - wallDistanceScaling);
        }

        public Vector2 TailHeldOffset()
        {
            return (TailHeldDistance * projectile.scale * projectile.velocity);
        }

        public Vector2 TailStart()
        {
            return projectile.position + offsetY + TailHeldOffset();
        }

        public Vector2 TailRecessionWallDistanceScaleOffset()
        {
            return TailRecession() * (1f - wallDistanceScaling) * projectile.velocity;
        }

        public float TailRecession()
        {
            return (tailSize.Y + (beamSize.Y / 2f) - (tailSize.Y / 2f)) * projectile.scale;
        }

        public Vector2 TailEnd()
        {
            return TailStart() + (TailRecession() * projectile.velocity);
        }

        public float BodyExtension()
        {
            return Math.Max(0f, Distance);
        }

        public Vector2 BodyEnd()
        {
            return TailEnd() + BodyExtension() * projectile.velocity;
        }

        public float HeadExtension()
        {
            return (headSize.Y * 0.2f) * projectile.scale;
        }

        public Vector2 HeadEnd()
        {
            return BodyEnd() + HeadExtension() * projectile.velocity;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            return true;
        }

        public override bool? CanHitNPC(NPC target)
        {
            if (target.dontTakeDamage || target.friendly)
                return false;
            return CanHitEntity(target);
        }

        public override bool CanHitPlayer(Player target)
        {
            if (target.immune)
                return false;
            return CanHitEntity(target);
        }

        public override bool CanHitPvp(Player target)
        {
            if (target.immune || target.hostile || target.team == Main.player[projectile.owner].team)
                return false;
            return CanHitEntity(target);
        }

        public float TrueDistance()
        {
            return (BodyExtension() + HeadExtension() + TailRecession());
        }

        public bool CanHitEntity(Entity e)
        {
            if (!e.active)
                return false;
            Tuple<bool, float> collisionData = ProjectileHelper.GetCollisionData(TailStart(), TailEnd(), BodyEnd(), HeadEnd(), tailSize.X, beamSize.X, headSize.X, maxBeamDistance, e.Hitbox);
            bool isAnyCollision = collisionData.Item1;
            float collisionDistance = collisionData.Item2;
            if (isAnyCollision && isEntityColliding)
            {
                if (collisionDistance < TrueDistance())
                {
                    Distance = (collisionDistance + beamSpeed); // arbitrary padding
                }
                ProjectileHelper.DoBeamCollisionDust(dustType, collisionDustFrequency, projectile.velocity, HeadEnd());
            }

            return isAnyCollision;
        }

        // Set custom immunity time on hitting an NPC
        public override void OnHitNPC(NPC target, int damage, float knockback, bool crit)
        {
            base.OnHitNPC(target, damage, knockback, crit);
            target.immune[projectile.owner] = immunityFrameOverride;            
        }

        // helper field lets us limit mouse movement's impact on the charge ball rotation.
        private Vector2 _oldMouseVector = Vector2.Zero;

        // the old screen position helps us offset the MouseWorld vector by our screen position so it's more stable.
        private Vector2 _oldScreenPosition = Vector2.Zero;

        // Just fired bool is true the moment the beam comes into existence, to process audio, and then immediately set to false afterwards to prevent sound from looping.
        private bool _justFired = true;

        // used to track the original mouse vector for beams that don't track at all.
        private Vector2 _originalMouseVector = Vector2.Zero;
        private Vector2 _originalScreenPosition = Vector2.Zero;

        private float lazyLastStruckTileDistance = float.MaxValue;
        // The AI of the projectile
        public override void AI()
        {
            Player player = Main.player[projectile.owner];

            ProcessKillRoutine(player);

            // stationary beams are instantaneously "detached", they behave weirdly.
            if (isStationaryBeam && !IsDetached)
            {
                DetachmentTimer = 1;
            }

            // capture the current mouse vector, we're going to normalize movement prior to updating the charge ball location.
            if (projectile.owner == Main.myPlayer)
            {
                Vector2 mouseVector = Main.MouseWorld;

                if (_originalMouseVector == Vector2.Zero)
                {
                    _originalMouseVector = mouseVector;
                }

                if (isStationaryBeam && _originalMouseVector != Vector2.Zero)
                {
                    mouseVector = _originalMouseVector;
                }

                Vector2 screenPosition = Main.screenPosition;

                if (_originalScreenPosition == Vector2.Zero)
                {
                    _originalScreenPosition = screenPosition;
                }

                if (isStationaryBeam && _originalScreenPosition != Vector2.Zero)
                {
                    screenPosition = _originalScreenPosition;
                }

                if (_oldMouseVector != Vector2.Zero && !isStationaryBeam)
                {
                    Vector2 mouseMovementVector = (mouseVector - _oldMouseVector) / rotationSlowness;
                    Vector2 screenChange = screenPosition - _oldScreenPosition;
                    mouseVector = _oldMouseVector + mouseMovementVector + screenChange;
                }

                UpdateBeamTailLocationAndDirection(player, mouseVector);

                _oldMouseVector = mouseVector;

                _oldScreenPosition = screenPosition;
            }

            UpdateBeamPlayerItemUse(player);

            // handle animation frames on animated beams
            if (isBeamSegmentAnimated)
            {
                _beamSegmentAnimation += 8;
                if (_beamSegmentAnimation >= beamSize.Y)
                {
                    _beamSegmentAnimation = 0;
                }
            }

            bool isColliding = false;
            Rectangle struckTile = ProjectileHelper.GetClosestTileCollisionInRay(TailStart(), HeadEnd());
            // lazy strats for fixing weird beam issues start here, warning hax.
            if (!struckTile.Equals(Rectangle.Empty))
            {
                Tuple<bool, float> collisionData = ProjectileHelper.GetCollisionData(TailStart(), Vector2.Zero, Vector2.Zero, HeadEnd(), tailSize.X, beamSize.X, headSize.X, Distance, struckTile);
                isColliding = collisionData.Item1;
                Distance = Math.Min(maxBeamDistance, (collisionData.Item2 - HeadExtension()));
            }

            // if distance is about to be throttled, we're hitting something. Spawn some dust.
            if (isColliding)
            {
                var dustVector = HeadEnd();
                ProjectileHelper.DoBeamCollisionDust(dustType, collisionDustFrequency, projectile.velocity, dustVector);}
            else
            {
                Distance = Math.Min(maxBeamDistance, (Distance + beamSpeed));
                DebugHelper.Log($"Not colliding: Projectile distance set to {Distance}");
            }

            // shrinks the beam when it's near a wall, this looks like crap at the moment.
            //if (Distance <= Vector2.Distance(TailStart(), HeadEnd()))
            //{
            //    if (Distance > 0f)
            //        wallDistanceScaling = Distance / Vector2.Distance(TailStart(), HeadEnd());
            //}
            //else if (!IsDetached)
            //{
            //    wallDistanceScaling = Math.Min(1.0f, projectile.scale * 1.1f);
            //}

            // shoot sweet sweet particles
            for (var i = 0; i < fireParticleDensity; i++)
            {
                ProjectileHelper.DoBeamDust(projectile.position, projectile.velocity, dustType, dustFrequency, Distance, TailHeldDistance, tailSize.ToVector2(), beamSpeed);
            }

            // Handle the audio playing, note this positionally tracks at the head position end for effect.
            if (_justFired)
            {
                beamSoundSlotId = SoundHelper.PlayCustomSound(beamSoundKey, HeadEnd());
            }

            _justFired = false;

            // Update tracked audio
            SoundHelper.UpdateTrackedSound(beamSoundSlotId, HeadEnd());

            //Add lights
            DelegateMethods.v3_1 = new Vector3(0.8f, 0.8f, 1f);
            Utils.PlotTileLine(projectile.Center, projectile.Center + projectile.velocity * (Distance - TailHeldDistance), beamSize.Y, DelegateMethods.CastLight);
        }

        public void UpdateBeamTailLocationAndDirection(Player player, Vector2 mouseVector)
        {
            // server has no business running this code.
            if (Main.netMode == NetmodeID.Server)
                return;

            // Multiplayer support here, only run this code if the client running it is the owner of the projectile
            if (projectile.owner == Main.myPlayer && (!IsDetached || isStationaryBeam))
            {
                Vector2 diff = mouseVector - projectile.position;
                diff.Normalize();
                projectile.velocity = diff;
                projectile.direction = mouseVector.X > projectile.position.X ? 1 : -1;
                projectile.netUpdate = true;
            }
        }

        public void ProcessKillRoutine(Player player)
        {
            projectile.timeLeft = 2;
            MyPlayer modPlayer = player.GetModPlayer<MyPlayer>();

            if (!modPlayer.isMouseLeftHeld)
            {
                projectile.StartKillRoutine();
            }

            if (IsDetached && FiringTime == 0)
            {
                DetachmentTimer++;
                projectile.scale /= 1.1f;
            }

            if (FiringTime > 0)
            {
                FiringTime--;
            }

            if (player.dead || DetachmentTimer >= beamFadeOutTime)
            {
                projectile.Kill();
            }
        }

        // helper flag  used to set the initial position of a detached beam once and only once before removing it from the player "anchor point"
        private bool _isAttachedOnce = false;
        public void UpdateBeamPlayerItemUse(Player player)
        {
            // skip this entire routine if the detachment timer is greater than 0
            if (IsDetached)
            {
                if (!_isAttachedOnce)
                {
                    projectile.position = player.Center;
                    _isAttachedOnce = true;
                }
                return;
            }

            MyPlayer modPlayer = player.GetModPlayer<MyPlayer>();
            projectile.position = player.Center;
            int dir = projectile.direction;
            player.ChangeDir(dir);
            player.heldProj = projectile.whoAmI;
            if (modPlayer.isMouseLeftHeld)
            {
                player.itemTime = 2;
                player.itemAnimation = 2;
            }
            player.itemRotation = (float)Math.Atan2(projectile.velocity.Y * dir, projectile.velocity.X * dir);
        }

        public override void CutTiles()
        {
            DelegateMethods.tilecut_0 = TileCuttingContext.AttackProjectile;
            Vector2 unit = projectile.velocity;
            Utils.PlotTileLine(TailStart(), TailStart() + unit * (Distance + headSize.Y * 0.66f), (beamSize.Y) * projectile.scale, DelegateMethods.CutTiles);
        }

        public override bool ShouldUpdatePosition()
        {
            return false;
        }

        public override void ModifyHitNPC(NPC target, ref int damage, ref float knockback, ref bool crit, ref int hitDirection)
        {
            base.ModifyHitNPC(target, ref damage, ref knockback, ref crit, ref hitDirection);
            //damage = GetPlayerKiDamageAfterMultipliers(damage);
        }

        public override void ModifyHitPlayer(Player target, ref int damage, ref bool crit)
        {
            base.ModifyHitPlayer(target, ref damage, ref crit);
            damage = GetPvpDamageReduction(damage);
            //damage = GetPlayerKiDamageAfterMultipliers(damage);
        }

        public override void ModifyHitPvp(Player target, ref int damage, ref bool crit)
        {
            base.ModifyHitPvp(target, ref damage, ref crit);
            damage = GetPvpDamageReduction(damage);
            //damage = GetPlayerKiDamageAfterMultipliers(damage);
        }

        public int GetPvpDamageReduction(int damage)
        {
            return (int)Math.Ceiling(damage / 2f);
        }

        public MyPlayer GetPlayerOwner()
        {
            var player = Main.player[projectile.owner];
            if (player != null)
            {
                return player.GetModPlayer<MyPlayer>();
            }
            return null;
        }

        //public int GetPlayerKiDamageAfterMultipliers(int damage)
        //{
        //    if (GetPlayerOwner() == null)
        //        return damage;
        //    float kiMultiplier = GetPlayerOwner().KiDamage;
        //    return (int)Math.Ceiling(damage * kiMultiplier);
        //}
    }
}